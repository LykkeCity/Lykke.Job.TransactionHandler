using System;
using System.Threading.Tasks;
using Common;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.SolarCoin;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.OperationsRepository.AutorestClient.Models;

namespace Lykke.Job.TransactionHandler.Queues.Messaging
{
    public class CashInOutCommandHandler
    {
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IClientSettingsRepository _clientSettingsRepository;

        public CashInOutCommandHandler(IAssetsServiceWithCache assetsServiceWithCache, IClientSettingsRepository clientSettingsRepository)
        {
            _assetsServiceWithCache = assetsServiceWithCache;
            _clientSettingsRepository = clientSettingsRepository;
        }

        public async Task Handle(ExternalCashInCommand command, IEventPublisher eventPublisher)
        {
            
        }

        private async Task<bool> ProcessDestroy(IBitcoinTransaction transaction, CashInOutQueueMessage msg)
        {
            var amount = msg.Amount.ParseAnyDouble();
            //Get uncolor context data
            var context = await _bitcoinTransactionService.GetTransactionContext<UncolorContextData>(transaction.TransactionId);

            //Register cash operation
            var cashOperationId = await _cashOperationsRepositoryClient
                .RegisterAsync(new CashInOutOperation
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ClientId = msg.ClientId,
                    Multisig = context.AddressFrom,
                    AssetId = msg.AssetId,
                    Amount = -Math.Abs(amount),
                    DateTime = DateTime.UtcNow,
                    AddressFrom = context.AddressFrom,
                    AddressTo = context.AddressTo,
                    TransactionId = msg.Id
                });

            //Update context data
            context.CashOperationId = cashOperationId;
            var contextJson = context.ToJson();
            var cmd = new DestroyCommand
            {
                Context = contextJson,
                Amount = Math.Abs(amount),
                AssetId = msg.AssetId,
                Address = context.AddressFrom,
                TransactionId = Guid.Parse(msg.Id)
            };

            await _bitcoinTransactionsRepository.UpdateAsync(transaction.TransactionId, cmd.ToJson(), null, "");

            await _bitcoinTransactionService.SetTransactionContext(transaction.TransactionId, context);

            //Send to bitcoin
            await _bitcoinCommandSender.SendCommand(cmd);

            return true;
        }

        private async Task<bool> ProcessManualOperation(IBitcoinTransaction transaction, CashInOutQueueMessage msg)
        {
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(msg.AssetId);
            var walletCredentials = await _walletCredentialsRepository.GetAsync(msg.ClientId);
            var context = transaction.GetContextData<CashOutContextData>();
            var isOffchainClient = await _clientSettingsRepository.IsOffchainClient(msg.ClientId);
            var isBtcOffchainClient = isOffchainClient && asset.Blockchain == Blockchain.Bitcoin;

            var operation = new CashInOutOperation
            {
                Id = Guid.NewGuid().ToString(),
                ClientId = msg.ClientId,
                Multisig = walletCredentials.MultiSig,
                AssetId = msg.AssetId,
                Amount = msg.Amount.ParseAnyDouble(),
                DateTime = DateTime.UtcNow,
                AddressFrom = walletCredentials.MultiSig,
                AddressTo = context.Address,
                TransactionId = msg.Id,
                Type = CashOperationType.None,
                BlockChainHash = asset.IssueAllowed && isBtcOffchainClient ? string.Empty : transaction.BlockchainHash,
                State = GetState(transaction, isBtcOffchainClient)
            };

            try
            {
                await _cashOperationsRepositoryClient.RegisterAsync(operation);
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(CashInOutQueue), nameof(ProcessManualOperation), null, e);
                return false;
            }

            return true;
        }

        private async Task<bool> ProcessCashOut(IBitcoinTransaction transaction, CashInOutQueueMessage msg)
        {
            //Get client wallet
            var walletCredentials = await _walletCredentialsRepository
                .GetAsync(msg.ClientId);
            var amount = msg.Amount.ParseAnyDouble();
            var context = await _bitcoinTransactionService.GetTransactionContext<CashOutContextData>(transaction.TransactionId);

            var asset = await _assetsServiceWithCache.TryGetAssetAsync(msg.AssetId);

            var isOffchainClient = await _clientSettingsRepository.IsOffchainClient(msg.ClientId);
            var isBtcOffchainClient = isOffchainClient && asset.Blockchain == Blockchain.Bitcoin;

            bool isForwardWithdawal = context.AddData?.ForwardWithdrawal != null;
            if (isForwardWithdawal)
            {
                var baseAsset = await _assetsServiceWithCache.TryGetAssetAsync(asset.ForwardBaseAsset);

                var forwardCashInId = await _cashOperationsRepositoryClient
                    .RegisterAsync(new CashInOutOperation
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClientId = msg.ClientId,
                        Multisig = walletCredentials.MultiSig,
                        AssetId = baseAsset.Id,
                        Amount = Math.Abs(amount),
                        DateTime = DateTime.UtcNow.AddDays(asset.ForwardFrozenDays),
                        AddressFrom = walletCredentials.MultiSig,
                        AddressTo = context.Address,
                        TransactionId = msg.Id,
                        Type = CashOperationType.ForwardCashIn,
                        State = isBtcOffchainClient ? TransactionStates.InProcessOffchain : TransactionStates.InProcessOnchain
                    });

                await _forwardWithdrawalRepository.SetLinkedCashInOperationId(msg.ClientId, context.AddData.ForwardWithdrawal.Id,
                    forwardCashInId);
            }

            //Register cash operation
            string cashOperationId = await _cashOperationsRepositoryClient
                .RegisterAsync(new CashInOutOperation
                {
                    Id = Guid.NewGuid().ToString(),
                    ClientId = msg.ClientId,
                    Multisig = walletCredentials.MultiSig,
                    AssetId = msg.AssetId,
                    Amount = -Math.Abs(amount),
                    DateTime = DateTime.UtcNow,
                    AddressFrom = walletCredentials.MultiSig,
                    AddressTo = context.Address,
                    TransactionId = msg.Id,
                    Type = isForwardWithdawal ? CashOperationType.ForwardCashOut : CashOperationType.None,
                    BlockChainHash = asset.IssueAllowed && isBtcOffchainClient ? string.Empty : transaction.BlockchainHash,
                    State = isForwardWithdawal ? TransactionStates.SettledOffchain : GetState(transaction, isBtcOffchainClient)
                });

            //Update context data
            context.CashOperationId = cashOperationId;
            var contextJson = context.ToJson();
            var cmd = new CashOutCommand
            {
                Amount = Math.Abs(amount),
                AssetId = msg.AssetId,
                Context = contextJson,
                SourceAddress = walletCredentials.MultiSig,
                DestinationAddress = context.Address,
                TransactionId = Guid.Parse(transaction.TransactionId)
            };

            await _bitcoinTransactionsRepository.UpdateAsync(transaction.TransactionId, cmd.ToJson(), null, "");

            await _bitcoinTransactionService.SetTransactionContext(transaction.TransactionId, context);

            if (asset.Blockchain == Blockchain.Ethereum)
            {
                string errMsg = string.Empty;

                if (asset.Type == AssetType.Erc20Token)
                {
                    try
                    {
                        var response = await _srvEthereumHelper.HotWalletCashoutAsync(transaction.TransactionId,
                            _settings.HotwalletAddress,
                            context.Address,
                            (decimal)Math.Abs(amount),
                            asset);

                        if (response.HasError)
                            errMsg = response.Error.ToJson();
                    }
                    catch (Exception e)
                    {
                        errMsg = $"{e.GetType()}\n{e.Message}";
                    }
                }
                else
                {
                    try
                    {
                        var address = await _bcnClientCredentialsRepository.GetClientAddress(msg.ClientId);
                        var txRequest =
                            await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(transaction.TransactionId));

                        txRequest.OperationIds = new[] { cashOperationId };
                        await _ethereumTransactionRequestRepository.UpdateAsync(txRequest);

                        var response = await _srvEthereumHelper.SendCashOutAsync(txRequest.Id,
                            txRequest.SignedTransfer.Sign,
                            asset, address, txRequest.AddressTo,
                            txRequest.Volume);

                        if (response.HasError)
                            errMsg = response.Error.ToJson();
                    }
                    catch (Exception e)
                    {
                        errMsg = $"{e.GetType()}\n{e.Message}";
                    }
                }

                if (!string.IsNullOrEmpty(errMsg))
                {
                    await _ethClientEventLogs.WriteEvent(msg.ClientId, Event.Error,
                        new { Request = transaction.TransactionId, Error = errMsg }.ToJson());
                }
            }

            if (asset.Id == LykkeConstants.SolarAssetId)
                await ProcessSolarCashOut(msg.ClientId, context.Address, Math.Abs(amount), transaction.TransactionId);

            if (asset.Id == LykkeConstants.ChronoBankAssetId)
                await ProcessChronoBankCashOut(context.Address, Math.Abs(amount), transaction.TransactionId);

            if (asset.Blockchain == Blockchain.Bitcoin && asset.IsTrusted && asset.BlockchainWithdrawal && !isForwardWithdawal)
                await ProcessBitcoinCashOut(asset, context.Address, (decimal)Math.Abs(amount), transaction.TransactionId);

            return true;
        }

        private static TransactionStates GetState(
            IBitcoinTransaction transaction, bool isBtcOffchainClient)
        {
            return isBtcOffchainClient
                ? (string.IsNullOrWhiteSpace(transaction.BlockchainHash)
                    ? TransactionStates.SettledOffchain
                    : TransactionStates.SettledOnchain)
                : (string.IsNullOrWhiteSpace(transaction.BlockchainHash)
                    ? TransactionStates.InProcessOnchain
                    : TransactionStates.SettledOnchain);
        }

        private async Task<bool> ProcessIssue(IBitcoinTransaction transaction, CashInOutQueueMessage msg)
        {
            var isOffchain = await _clientSettingsRepository.IsOffchainClient(msg.ClientId);

            //Get client wallet
            var walletCredentials = await _walletCredentialsRepository
                .GetAsync(msg.ClientId);
            var amount = msg.Amount.ParseAnyDouble();
            var context = await _bitcoinTransactionService.GetTransactionContext<IssueContextData>(transaction.TransactionId);
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(msg.AssetId);

            //Register cash operation
            var cashOperationId = await _cashOperationsRepositoryClient
                .RegisterAsync(new CashInOutOperation
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ClientId = msg.ClientId,
                    Multisig = walletCredentials.MultiSig,
                    AssetId = msg.AssetId,
                    Amount = Math.Abs(amount),
                    DateTime = DateTime.UtcNow,
                    AddressTo = walletCredentials.MultiSig,
                    TransactionId = transaction.TransactionId,
                    State = asset.IsTrusted ? TransactionStates.SettledOffchain : TransactionStates.InProcessOffchain
                });

            context.CashOperationId = cashOperationId;
            var contextJson = context.ToJson();
            var cmd = new Core.Domain.BitCoin.IssueCommand
            {
                Amount = amount,
                AssetId = msg.AssetId,
                Multisig = walletCredentials.MultiSig,
                Context = contextJson,
                TransactionId = Guid.Parse(transaction.TransactionId)
            };

            await _bitcoinTransactionsRepository.UpdateAsync(transaction.TransactionId, cmd.ToJson(), null, "");

            await _bitcoinTransactionService.SetTransactionContext(transaction.TransactionId, context);

            var isClientTrusted = await _clientAccountClient.IsTrustedAsync(msg.ClientId);

            if (isClientTrusted.Value || asset.IsTrusted)
                return true;

            await _offchainRequestService.CreateOffchainRequestAndNotify(transaction.TransactionId, msg.ClientId, msg.AssetId, (decimal)amount, null, OffchainTransferType.CashinToClient);

            return true;
        }

        private Task ProcessChronoBankCashOut(string address, double amount, string txId)
        {
            return _chronoBankService.SendCashOutRequest(txId, address, amount);
        }

        private async Task ProcessSolarCashOut(string clientId, string address, double amount, string txId)
        {
            var slrAddress = new SolarCoinAddress(address);
            var clientAcc = await _clientAccountClient.GetByIdAsync(clientId);

            var sendEmailTask = _srvEmailsFacade.SendSolarCashOutCompletedEmail(clientAcc.PartnerId, clientAcc.Email, slrAddress.Value, amount);
            var solarRequestTask = _srvSolarCoinHelper.SendCashOutRequest(txId, slrAddress, amount);

            await Task.WhenAll(sendEmailTask, solarRequestTask);
        }

        private Task ProcessBitcoinCashOut(Asset asset, string address, decimal amount, string transactionId)
        {
            return _bitcoinApiClient.CashoutAsync(new Bitcoin.Api.Client.BitcoinApi.Models.CashoutModel
            {
                Amount = amount,
                AssetId = asset.Id,
                DestinationAddress = address,
                TransactionId = Guid.Parse(transactionId)
            });
        }
    }
}