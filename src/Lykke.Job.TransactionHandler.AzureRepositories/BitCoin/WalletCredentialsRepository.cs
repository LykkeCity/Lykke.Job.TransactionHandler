using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Job.TransactionHandler.AzureRepositories.BitCoin
{
    public class WalletCredentialsEntity : TableEntity, IWalletCredentials
    {
        public static class ByClientId
        {
            public static string GeneratePartitionKey()
            {
                return "Wallet";
            }

            public static string GenerateRowKey(string clientId)
            {
                return clientId;
            }

            public static WalletCredentialsEntity CreateNew(IWalletCredentials src)
            {
                var entity = Create(src);
                entity.PartitionKey = GeneratePartitionKey();
                entity.RowKey = GenerateRowKey(src.ClientId);
                return entity;
            }
        }

        public static class ByColoredMultisig
        {
            public static string GeneratePartitionKey()
            {
                return "WalletColoredMultisig";
            }

            public static string GenerateRowKey(string coloredMultisig)
            {
                return coloredMultisig;
            }

            public static WalletCredentialsEntity CreateNew(IWalletCredentials src)
            {
                var entity = Create(src);
                entity.PartitionKey = GeneratePartitionKey();
                entity.RowKey = GenerateRowKey(src.ColoredMultiSig);
                return entity;
            }
        }

        public static class ByMultisig
        {
            public static string GeneratePartitionKey()
            {
                return "WalletMultisig";
            }

            public static string GenerateRowKey(string multisig)
            {
                return multisig;
            }

            public static WalletCredentialsEntity CreateNew(IWalletCredentials src)
            {
                var entity = Create(src);
                entity.PartitionKey = GeneratePartitionKey();
                entity.RowKey = GenerateRowKey(src.MultiSig);
                return entity;
            }
        }

        public static class ByEthContract
        {
            public static string GeneratePartitionKey()
            {
                return "EthConversionWallet";
            }

            public static string GenerateRowKey(string ethWallet)
            {
                return ethWallet;
            }

            public static WalletCredentialsEntity CreateNew(IWalletCredentials src)
            {
                var entity = Create(src);
                entity.PartitionKey = GeneratePartitionKey();
                entity.RowKey = GenerateRowKey(src.EthConversionWalletAddress);
                return entity;
            }
        }

        public static class BySolarCoinWallet
        {
            public static string GeneratePartitionKey()
            {
                return "SolarCoinWallet";
            }

            public static string GenerateRowKey(string address)
            {
                return address;
            }

            public static WalletCredentialsEntity CreateNew(IWalletCredentials src)
            {
                var entity = Create(src);
                entity.PartitionKey = GeneratePartitionKey();
                entity.RowKey = GenerateRowKey(src.SolarCoinWalletAddress);
                return entity;
            }
        }
        
        public static WalletCredentialsEntity Create(IWalletCredentials src)
        {
            return new WalletCredentialsEntity
            {
                ClientId = src.ClientId,
                PrivateKey = src.PrivateKey,
                Address = src.Address,
                MultiSig = src.MultiSig,
                ColoredMultiSig = src.ColoredMultiSig,
                PreventTxDetection = src.PreventTxDetection,
                EncodedPrivateKey = src.EncodedPrivateKey,
                PublicKey = src.PublicKey,
                BtcConvertionWalletPrivateKey = src.BtcConvertionWalletPrivateKey,
                BtcConvertionWalletAddress = src.BtcConvertionWalletAddress,
                EthConversionWalletAddress = src.EthConversionWalletAddress,
                EthAddress = src.EthAddress,
                EthPublicKey = src.EthPublicKey,
                SolarCoinWalletAddress = src.SolarCoinWalletAddress,
                ChronoBankContract = src.ChronoBankContract,
                QuantaContract = src.QuantaContract
            };
        }
        
        public string ClientId { get; set; }
        public string Address { get; set; }
        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }
        public string MultiSig { get; set; }
        public string ColoredMultiSig { get; set; }
        public bool PreventTxDetection { get; set; }
        public string EncodedPrivateKey { get; set; }
        public string BtcConvertionWalletPrivateKey { get; set; }
        public string BtcConvertionWalletAddress { get; set; }
        public string EthConversionWalletAddress { get; set; }
        public string EthAddress { get; set; }
        public string EthPublicKey { get; set; }
        public string SolarCoinWalletAddress { get; set; }
        public string ChronoBankContract { get; set; }
        public string QuantaContract { get; set; }
    }

    public class WalletCredentialsRepository : IWalletCredentialsRepository
    {
        private readonly INoSQLTableStorage<WalletCredentialsEntity> _tableStorage;

        public WalletCredentialsRepository(INoSQLTableStorage<WalletCredentialsEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }
        
        public async Task<IWalletCredentials> GetAsync(string clientId)
        {
            var partitionKey = WalletCredentialsEntity.ByClientId.GeneratePartitionKey();
            var rowKey = WalletCredentialsEntity.ByClientId.GenerateRowKey(clientId);

            var entity = await _tableStorage.GetDataAsync(partitionKey, rowKey);

            if (entity == null)
                return null;

            return string.IsNullOrEmpty(entity.MultiSig) ? null : entity;
        }        
    }

}