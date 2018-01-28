using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.BitCoin
{
    public interface IWalletCredentials
    {
        string ClientId { get; }
        string Address { get; }
        string PublicKey { get; }
        string PrivateKey { get; }
        string MultiSig { get; }
        string ColoredMultiSig { get; }
        bool PreventTxDetection { get; }
        string EncodedPrivateKey { get; }

        /// <summary>
        /// Conversion wallet is used for accepting BTC deposit and transfering needed LKK amount
        /// </summary>
        string BtcConvertionWalletPrivateKey { get; set; }
        string BtcConvertionWalletAddress { get; set; }

        /// <summary>
        /// Eth contract for user
        /// </summary>
        //ToDo: rename field to EthContract and change existing records
        string EthConversionWalletAddress { get; set; }
        string EthAddress { get; set; }
        string EthPublicKey { get; set; }

        string SolarCoinWalletAddress { get; set; }

        string ChronoBankContract { get; set; }

        string QuantaContract { get; set; }
    }

    public class WalletCredentials : IWalletCredentials
    {
        public string ClientId { get; set; }
        public string Address { get; set; }
        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }
        public string MultiSig { get; set; }
        public string ColoredMultiSig { get; set; }
        public bool PreventTxDetection { get; set; }
        public string EncodedPrivateKey { get; set; }

        /// <summary>
        /// Conversion wallet is used for accepting BTC deposit and transfering needed LKK amount
        /// </summary>
        public string BtcConvertionWalletPrivateKey { get; set; }
        public string BtcConvertionWalletAddress { get; set; }

        //EthContract in fact. ToDo: rename
        public string EthConversionWalletAddress { get; set; }
        public string EthAddress { get; set; }
        public string EthPublicKey { get; set; }

        public string SolarCoinWalletAddress { get; set; }
        public string ChronoBankContract { get; set; }
        public string QuantaContract { get; set; }
        
        public static WalletCredentials Create(IWalletCredentials src)
        {
            return new WalletCredentials
            {
                ClientId = src.ClientId,
                Address = src.Address,
                PrivateKey = src.PrivateKey,
                MultiSig = src.MultiSig,
                ColoredMultiSig = src.ColoredMultiSig,
                PreventTxDetection = src.PreventTxDetection,
                EncodedPrivateKey = src.EncodedPrivateKey,
                PublicKey = src.PublicKey,
                BtcConvertionWalletPrivateKey = src.BtcConvertionWalletPrivateKey,
                BtcConvertionWalletAddress = src.BtcConvertionWalletAddress,
                EthConversionWalletAddress = src.EthConversionWalletAddress,
                EthAddress = src.EthAddress,
                EthPublicKey = src.EthPublicKey
            };
        }
    }

    public interface IWalletCredentialsRepository
    {        
        Task<IWalletCredentials> GetAsync(string clientId);        
    }
}
