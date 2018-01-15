using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Events.Ethereum;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.Operations.Client;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class EthereumCommandHandler
    {
        private readonly ILog _log;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly ISrvEthereumHelper _srvEthereumHelper;
        private readonly IBcnClientCredentialsRepository _bcnClientCredentialsRepository;
        private readonly IEthClientEventLogs _ethClientEventLogs;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly AppSettings.EthereumSettings _settings;
        private readonly TimeSpan _retryTimeout;
        private readonly IOperationsClient _operationsClient;

        public EthereumCommandHandler(
            [NotNull] ILog log,
            [NotNull] IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            [NotNull] ISrvEthereumHelper srvEthereumHelper,
            [NotNull] IBcnClientCredentialsRepository bcnClientCredentialsRepository,
            [NotNull] IEthClientEventLogs ethClientEventLogs,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] IOperationsClient operationsClient,
            [NotNull] AppSettings.EthereumSettings settings,
            TimeSpan retryTimeout)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository ?? throw new ArgumentNullException(nameof(ethereumTransactionRequestRepository));
            _srvEthereumHelper = srvEthereumHelper ?? throw new ArgumentNullException(nameof(srvEthereumHelper));
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository ?? throw new ArgumentNullException(nameof(bcnClientCredentialsRepository));
            _ethClientEventLogs = ethClientEventLogs ?? throw new ArgumentNullException(nameof(ethClientEventLogs));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _operationsClient = operationsClient ?? throw new ArgumentNullException(nameof(operationsClient));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _retryTimeout = retryTimeout;
        }

        public async Task<CommandHandlingResult> Handle(Commands.ProcessEthereumCashoutCommand command)
        {
            await _log.WriteInfoAsync(nameof(EthereumCommandHandler), nameof(Commands.ProcessEthereumCashoutCommand), command.ToJson(), "");

            ChaosKitty.Meow();

            var asset = await _assetsServiceWithCache.TryGetAssetAsync(command.AssetId);
            string errorMessage = null;

            if (asset.Type == AssetType.Erc20Token)
            {
                var response = await _srvEthereumHelper.HotWalletCashoutAsync(
                    command.TransactionId,
                    _settings.HotwalletAddress,
                    command.Address,
                    (decimal)Math.Abs(command.Amount),
                    asset);

                if (response.HasError)
                    errorMessage = response.Error.ToJson();
            }
            else
            {
                var transactionId = Guid.Parse(command.TransactionId);
                if (!asset.IsTrusted)
                {
                    var address = await _bcnClientCredentialsRepository.GetClientAddress(command.ClientId);
                    var txRequest = await _ethereumTransactionRequestRepository.GetAsync(transactionId);

                    txRequest.OperationIds = new[] { command.CashOperationId };
                    await _ethereumTransactionRequestRepository.UpdateAsync(txRequest);

                    var response = await _srvEthereumHelper.SendCashOutAsync(
                        txRequest.Id,
                        txRequest.SignedTransfer.Sign,
                        asset,
                        address,
                        txRequest.AddressTo,
                        txRequest.Volume);

                    if (response.HasError)
                        errorMessage = response.Error.ToJson();
                }
                else
                {
                    var address = _settings.HotwalletAddress;

                    var response = await _srvEthereumHelper.SendCashOutAsync(
                        transactionId,
                        string.Empty,
                        asset,
                        address,
                        command.Address,
                        (decimal)Math.Abs(command.Amount));

                    if (response.HasError)
                        errorMessage = response.Error.ToJson();
                }
            }

            if (errorMessage != null)
            {
                await _ethClientEventLogs.WriteEvent(command.ClientId, Event.Error, new { Request = command.TransactionId, Error = errorMessage }.ToJson());
                await _log.WriteErrorAsync(nameof(EthereumCommandHandler), nameof(Commands.ProcessEthereumCashoutCommand), command.ToJson(), new Exception(errorMessage));
                return CommandHandlingResult.Fail(_retryTimeout);
            }

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(EthCreateTransactionRequestCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(EthereumCommandHandler), nameof(EthCreateTransactionRequestCommand), command.ToJson());

            ChaosKitty.Meow();

            var toAddress = await _bcnClientCredentialsRepository.GetClientAddress(command.ClientId);
            var id = new Guid(command.Id);

            var ethRequest = await _ethereumTransactionRequestRepository.GetAsync(id);

            if (ethRequest == null)
            {
                await _ethereumTransactionRequestRepository.InsertAsync(new EthereumTransactionRequest
                {
                    Id = id,
                    AddressTo = toAddress,
                    AssetId = command.AssetId,
                    ClientId = command.ClientId,
                    OperationIds = command.OperationIds,
                    OperationType = OperationType.Trade,
                    OrderId = command.OrderId,
                    Volume = command.Amount
                });
            }

            eventPublisher.PublishEvent(new EthTransactionRequestCreatedEvent
            {
                TransactionId = command.Id,
                OrderId = command.OrderId,
                ClientId = command.ClientId,
                AssetId = command.AssetId,
                Amount = command.Amount
            });

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(EthGuaranteeTransferCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(EthereumCommandHandler), nameof(EthGuaranteeTransferCommand), command.ToJson());

            ChaosKitty.Meow();

            var asset = await _assetsServiceWithCache.TryGetAssetAsync(command.Asset.Id);

            if (asset.IsTrusted)
            {
                return CommandHandlingResult.Ok();
            }

            try
            {
                var fromAddress = await _bcnClientCredentialsRepository.GetClientAddress(command.ClientId);
                var ethereumTxRequest = await _ethereumTransactionRequestRepository.GetByOrderAsync(command.OrderId);
                var change = ethereumTxRequest.Volume - Math.Abs(command.Amount);

                EthereumResponse<OperationResponse> res;
                var minAmountForAsset = (decimal)Math.Pow(10, -command.Asset.Accuracy);

                if (change > 0 && Math.Abs(change) >= minAmountForAsset)
                {
                    res = await _srvEthereumHelper.SendTransferWithChangeAsync(change,
                        ethereumTxRequest.SignedTransfer.Sign, ethereumTxRequest.SignedTransfer.Id,
                        command.Asset, fromAddress, _settings.HotwalletAddress, ethereumTxRequest.Volume);
                }
                else
                {
                    res = await _srvEthereumHelper.SendTransferAsync(ethereumTxRequest.SignedTransfer.Id, ethereumTxRequest.SignedTransfer.Sign,
                        command.Asset, fromAddress, _settings.HotwalletAddress, ethereumTxRequest.Volume);
                }

                if (res.HasError)
                {
                    if (res.Error.ErrorCode != ErrorCode.EntityAlreadyExists)
                        throw new ApplicationException(res.Error.ToJson());
                }
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(EthereumCommandHandler), nameof(EthGuaranteeTransferCommand), command.ToJson(), e);

                throw;
            }

            eventPublisher.PublishEvent(new EthGuaranteeTransferCompletedEvent { OrderId = command.OrderId });

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(EthBuyCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(EthereumCommandHandler), nameof(EthBuyCommand), command.ToJson());

            ChaosKitty.Meow();

            var asset = await _assetsServiceWithCache.TryGetAssetAsync(command.AssetId);

            if (asset.IsTrusted)
            {
                return CommandHandlingResult.Ok();
            }

            var toAddress = await _bcnClientCredentialsRepository.GetClientAddress(command.ClientId);
            var res = await _srvEthereumHelper.SendTransferAsync(new Guid(command.TransactionId), string.Empty, asset,
                _settings.HotwalletAddress, toAddress, command.Amount);

            if (res.HasError && res.Error.ErrorCode != ErrorCode.EntityAlreadyExists)
            {
                var errMsg = res.Error.ToJson();

                await _log.WriteWarningAsync(nameof(EthereumCommandHandler), nameof(EthBuyCommand), errMsg, string.Empty);

                throw new InvalidOperationException(errMsg);
            }

            eventPublisher.PublishEvent(new EthTransferCompletedEvent { OrderId = command.OrderId });

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(EthTransferTrustedWalletCommand command)
        {
            await _log.WriteInfoAsync(nameof(EthereumCommandHandler), nameof(EthTransferTrustedWalletCommand), command.ToJson());

            ChaosKitty.Meow();

            var transferType = command.TransferType;
            var txRequest = command.TxRequest;

            if (transferType == TransferType.BetweenTrusted)
                return CommandHandlingResult.Ok();

            try
            {
                var asset = await _assetsServiceWithCache.TryGetAssetAsync(txRequest.AssetId);
                var clientAddress = await _bcnClientCredentialsRepository.GetClientAddress(txRequest.ClientId);
                var hotWalletAddress = _settings.HotwalletAddress;

                string addressFrom;
                string addressTo;
                Guid transferId;
                string sign;
                switch (transferType)
                {
                    case TransferType.ToTrustedWallet:
                        addressFrom = clientAddress;
                        addressTo = hotWalletAddress;
                        transferId = txRequest.SignedTransfer.Id;
                        sign = txRequest.SignedTransfer.Sign;
                        break;
                    case TransferType.FromTrustedWallet:
                        addressFrom = hotWalletAddress;
                        addressTo = clientAddress;
                        transferId = txRequest.Id;
                        sign = string.Empty;
                        break;
                    default:
                        await _log.WriteErrorAsync(nameof(EthereumCommandHandler), nameof(EthTransferTrustedWalletCommand),
                            "Unknown transfer type", null);
                        return CommandHandlingResult.Ok(); // todo: Fail?
                }

                var ethResponse = await _srvEthereumHelper.SendTransferAsync(transferId, sign, asset, addressFrom,
                    addressTo, txRequest.Volume);

                if (ethResponse.HasError)
                {
                    await _log.WriteErrorAsync(nameof(EthereumCommandHandler), nameof(EthTransferTrustedWalletCommand), ethResponse.Error.ToJson(), null);
                    return CommandHandlingResult.Ok(); // todo: Fail?
                }

                await _operationsClient.Complete(transferId);
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(EthereumCommandHandler), nameof(EthTransferTrustedWalletCommand), e.Message, e);
                return CommandHandlingResult.Fail(TimeSpan.FromSeconds(20));
            }

            return CommandHandlingResult.Ok();
        }
    }
}