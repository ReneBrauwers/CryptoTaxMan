using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums
{

    public enum TransactionAssetType
    {
        crypto,
        stock,
        fiat,
        unknown
    }
 

    [Flags]
    public enum TransactionEventType
    {

        buy = 0,
        nftbuy = 1,
        transfer = 2,
        stake = 4,
        unstake = 8,
        pooladd = 16,
        poolremove = 32,
        sell = 64,
        nftsell = 128,
        transferfee = 256,
        unkonwn = 512

    }
}
