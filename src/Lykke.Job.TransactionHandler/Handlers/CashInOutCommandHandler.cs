﻿using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Bitcoin.Api.Client.BitcoinApi;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.ChronoBank;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.SolarCoin;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class CashInOutCommandHandler
    {
        private readonly ILog _log;
        private readonly IBitcoinCommandSender _bitcoinCommandSender;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IBitCoinTransactionsRepository _bitcoinTransactionsRepository;
        private readonly IForwardWithdrawalRepository _forwardWithdrawalRepository;
        private readonly IOffchainRequestService _offchainRequestService;
        private readonly IClientSettingsRepository _clientSettingsRepository;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly ISrvEthereumHelper _srvEthereumHelper;
        private readonly IBcnClientCredentialsRepository _bcnClientCredentialsRepository;
        private readonly IEthClientEventLogs _ethClientEventLogs;
        private readonly IBitcoinTransactionService _bitcoinTransactionService;
        private readonly AppSettings.EthereumSettings _settings;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ISrvEmailsFacade _srvEmailsFacade;
        private readonly ISrvSolarCoinHelper _srvSolarCoinHelper;
        private readonly IChronoBankService _chronoBankService;
        private readonly IBitcoinApiClient _bitcoinApiClient;

        public CashInOutCommandHandler(
            AppSettings.RabbitMqSettings config,
            ILog log,
            IBitcoinCommandSender bitcoinCommandSender,
            ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            IWalletCredentialsRepository walletCredentialsRepository,
            IBitCoinTransactionsRepository bitcoinTransactionsRepository,
            IForwardWithdrawalRepository forwardWithdrawalRepository,
            IOffchainRequestService offchainRequestService,
            IClientSettingsRepository clientSettingsRepository,
            IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            ISrvEthereumHelper srvEthereumHelper,
            IBcnClientCredentialsRepository bcnClientCredentialsRepository,
            IEthClientEventLogs ethClientEventLogs,
            IBitcoinTransactionService bitcoinTransactionService,
            IAssetsServiceWithCache assetsServiceWithCache,
            AppSettings.EthereumSettings settings,
            IClientAccountClient clientAccountClient,
            ISrvEmailsFacade srvEmailsFacade,
            ISrvSolarCoinHelper srvSolarCoinHelper,
            IChronoBankService chronoBankService,
            IBitcoinApiClient bitcoinApiClient)
        {
            _log = log;
            _bitcoinCommandSender = bitcoinCommandSender;
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient;
            _walletCredentialsRepository = walletCredentialsRepository;
            _bitcoinTransactionsRepository = bitcoinTransactionsRepository;
            _forwardWithdrawalRepository = forwardWithdrawalRepository;
            _offchainRequestService = offchainRequestService;
            _clientSettingsRepository = clientSettingsRepository;
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository;
            _srvEthereumHelper = srvEthereumHelper;
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository;
            _ethClientEventLogs = ethClientEventLogs;
            _bitcoinTransactionService = bitcoinTransactionService;
            _assetsServiceWithCache = assetsServiceWithCache;
            _settings = settings;
            _clientAccountClient = clientAccountClient;
            _srvEmailsFacade = srvEmailsFacade;
            _srvSolarCoinHelper = srvSolarCoinHelper;
            _chronoBankService = chronoBankService;
            _bitcoinApiClient = bitcoinApiClient;
        }

        public async Task<CommandHandlingResult> Handle(Commands.DestroyCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(CashInOutCommandHandler), nameof(Commands.DestroyCommand), command.ToJson(), "");

            var transactionId = command.TransactionId;
            var msg = command.Message;

            var amount = msg.Amount.ParseAnyDouble();
            //Get uncolor context data
            var context = await _bitcoinTransactionService.GetTransactionContext<UncolorContextData>(transactionId);

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
            var cmd = new Core.Domain.BitCoin.DestroyCommand
            {
                Context = contextJson,
                Amount = Math.Abs(amount),
                AssetId = msg.AssetId,
                Address = context.AddressFrom,
                TransactionId = Guid.Parse(msg.Id)
            };

            await _bitcoinTransactionsRepository.UpdateAsync(transactionId, cmd.ToJson(), null, "");

            await _bitcoinTransactionService.SetTransactionContext(transactionId, context);

            //Send to bitcoin
            await _bitcoinCommandSender.SendCommand(cmd);

            return CommandHandlingResult.Ok();
        }


        public async Task<CommandHandlingResult> Handle(Commands.ManualUpdateCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(CashInOutCommandHandler), nameof(Commands.ManualUpdateCommand), command.ToJson(), "");

            var addressTo = command.AddressTo;
            var transactionBlockchainHash = command.BlockchainHash;
            var msg = command.Message;


            var asset = await _assetsServiceWithCache.TryGetAssetAsync(msg.AssetId);
            var walletCredentials = await _walletCredentialsRepository.GetAsync(msg.ClientId);
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
                AddressTo = addressTo,
                TransactionId = msg.Id,
                Type = CashOperationType.None,
                BlockChainHash = asset.IssueAllowed && isBtcOffchainClient ? string.Empty : transactionBlockchainHash,
                State = GetTransactionState(transactionBlockchainHash, isBtcOffchainClient)
            };

            try
            {
                await _cashOperationsRepositoryClient.RegisterAsync(operation);
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(CashInOutCommandHandler), nameof(Commands.ManualUpdateCommand), command.ToJson(), e);
            }


            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(Commands.CashoutCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(CashInOutCommandHandler), nameof(Commands.CashoutCommand), command.ToJson(), "");

            var transactionId = command.TransactionId;
            var transactionBlockchainHash = command.BlockchainHash;
            var msg = command.Message;

            //Get client wallet
            var walletCredentials = await _walletCredentialsRepository
                .GetAsync(msg.ClientId);
            var amount = msg.Amount.ParseAnyDouble();
            var context = await _bitcoinTransactionService.GetTransactionContext<CashOutContextData>(transactionId);

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
                    BlockChainHash = asset.IssueAllowed && isBtcOffchainClient ? string.Empty : transactionBlockchainHash,
                    State = isForwardWithdawal ? TransactionStates.SettledOffchain : GetTransactionState(transactionBlockchainHash, isBtcOffchainClient)
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
                TransactionId = Guid.Parse(transactionId)
            };

            await _bitcoinTransactionsRepository.UpdateAsync(transactionId, cmd.ToJson(), null, "");

            await _bitcoinTransactionService.SetTransactionContext(transactionId, context);

            if (asset.Blockchain == Blockchain.Ethereum)
            {
                string errMsg = string.Empty;

                if (asset.Type == AssetType.Erc20Token)
                {
                    try
                    {
                        var response = await _srvEthereumHelper.HotWalletCashoutAsync(transactionId,
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
                            await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(transactionId));

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
                        new { Request = transactionId, Error = errMsg }.ToJson());
                }
            }

            if (asset.Id == LykkeConstants.SolarAssetId)
            {
                await ProcessSolarCashOut(msg.ClientId, context.Address, Math.Abs(amount), transactionId);
            }
            else if (asset.Id == LykkeConstants.ChronoBankAssetId)
            {
                await ProcessChronoBankCashOut(context.Address, Math.Abs(amount), transactionId);
            }
            else if (asset.Blockchain == Blockchain.Bitcoin && asset.IsTrusted && asset.BlockchainWithdrawal &&
                     !isForwardWithdawal)
            {
                await ProcessBitcoinCashOut(asset, context.Address, (decimal)Math.Abs(amount), transactionId);
            }

            return CommandHandlingResult.Ok();
        }

        private static TransactionStates GetTransactionState(
            string blockchainHash, bool isBtcOffchainClient)
        {
            return isBtcOffchainClient
                ? (string.IsNullOrWhiteSpace(blockchainHash)
                    ? TransactionStates.SettledOffchain
                    : TransactionStates.SettledOnchain)
                : (string.IsNullOrWhiteSpace(blockchainHash)
                    ? TransactionStates.InProcessOnchain
                    : TransactionStates.SettledOnchain);
        }

        public async Task<CommandHandlingResult> Handle(Commands.IssueCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(CashInOutCommandHandler), nameof(Commands.IssueCommand), command.ToJson(), "");

            var transactionId = command.TransactionId;
            var msg = command.Message;


            var isOffchain = await _clientSettingsRepository.IsOffchainClient(msg.ClientId);

            //Get client wallet
            var walletCredentials = await _walletCredentialsRepository
                .GetAsync(msg.ClientId);
            var amount = msg.Amount.ParseAnyDouble();
            var context = await _bitcoinTransactionService.GetTransactionContext<IssueContextData>(transactionId);
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
                    TransactionId = transactionId,
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
                TransactionId = Guid.Parse(transactionId)
            };

            await _bitcoinTransactionsRepository.UpdateAsync(transactionId, cmd.ToJson(), null, "");

            await _bitcoinTransactionService.SetTransactionContext(transactionId, context);

            var isClientTrusted = await _clientAccountClient.IsTrustedAsync(msg.ClientId);

            if (isClientTrusted.Value || asset.IsTrusted)
                return CommandHandlingResult.Ok();

            // todo: use CreateOffchainCashoutRequestCommand + refactor type
            await _offchainRequestService.CreateOffchainRequestAndNotify(transactionId, msg.ClientId, msg.AssetId, (decimal)amount, null, OffchainTransferType.CashinToClient);


            return CommandHandlingResult.Ok();
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