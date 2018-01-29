using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
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
        private readonly ISrvEthereumHelper _srvEthereumHelper;
        private readonly IEthClientEventLogs _ethClientEventLogs;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly AppSettings.EthereumSettings _settings;
        private readonly TimeSpan _retryTimeout;

        public EthereumCommandHandler(
            [NotNull] ILog log,
            [NotNull] ISrvEthereumHelper srvEthereumHelper,
            [NotNull] IEthClientEventLogs ethClientEventLogs,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] AppSettings.EthereumSettings settings,
            TimeSpan retryTimeout)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _srvEthereumHelper = srvEthereumHelper ?? throw new ArgumentNullException(nameof(srvEthereumHelper));
            _ethClientEventLogs = ethClientEventLogs ?? throw new ArgumentNullException(nameof(ethClientEventLogs));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _retryTimeout = retryTimeout;
        }

        public async Task<CommandHandlingResult> Handle(Commands.ProcessEthereumCashoutCommand command)
        {
            await _log.WriteInfoAsync(nameof(EthereumCommandHandler), nameof(Commands.ProcessEthereumCashoutCommand), command.ToJson(), "");

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

                if (response.HasError && response.Error.ErrorCode != ErrorCode.OperationWithIdAlreadyExists)
                    errorMessage = response.Error.ToJson();
            }
            else
            {
                var transactionId = Guid.Parse(command.TransactionId);
                var address = _settings.HotwalletAddress;

                var response = await _srvEthereumHelper.SendCashOutAsync(
                    transactionId,
                    string.Empty,
                    asset,
                    address,
                    command.Address,
                    (decimal)Math.Abs(command.Amount));

                if (response.HasError && response.Error.ErrorCode != ErrorCode.OperationWithIdAlreadyExists)
                    errorMessage = response.Error.ToJson();
            }

            ChaosKitty.Meow();

            if (errorMessage != null)
            {
                await _ethClientEventLogs.WriteEvent(command.ClientId, Event.Error, new { Request = command.TransactionId, Error = errorMessage }.ToJson());
                await _log.WriteErrorAsync(nameof(EthereumCommandHandler), nameof(Commands.ProcessEthereumCashoutCommand), command.ToJson(), new Exception(errorMessage));
                return CommandHandlingResult.Fail(_retryTimeout);
            }

            return CommandHandlingResult.Ok();
        }
    }
}