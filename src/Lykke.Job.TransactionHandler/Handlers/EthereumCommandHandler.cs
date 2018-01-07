using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;

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

        public EthereumCommandHandler(
            [NotNull] ILog log,
            [NotNull] IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            [NotNull] ISrvEthereumHelper srvEthereumHelper,
            [NotNull] IBcnClientCredentialsRepository bcnClientCredentialsRepository,
            [NotNull] IEthClientEventLogs ethClientEventLogs,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] AppSettings.EthereumSettings settings)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository ?? throw new ArgumentNullException(nameof(ethereumTransactionRequestRepository));
            _srvEthereumHelper = srvEthereumHelper ?? throw new ArgumentNullException(nameof(srvEthereumHelper));
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository ?? throw new ArgumentNullException(nameof(bcnClientCredentialsRepository));
            _ethClientEventLogs = ethClientEventLogs ?? throw new ArgumentNullException(nameof(ethClientEventLogs));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
            }

            return CommandHandlingResult.Ok();
        }

    }
}