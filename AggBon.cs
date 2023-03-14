using CatSdk.CryptoTypes;
using CatSdk.Facade;
using CatSdk.Symbol;
using CatSdk.Symbol.Factory;
using CatSdk.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace nagexym
{
    public class AggBon
    {
        public class TransactionStatusResponse
        {
            public string Group { get; set; }
            public string Code { get; set; }
            public string Hash { get; set; }
            public string Deadline { get; set; }
            public string Height { get; set; }
        }

        public AggBon() { }

        public async Task SendAggregateBonded()
        {
            // TESTNET
            var node = "http://160.248.184.223:3000";
            var alicePrivateKey = new PrivateKey("PRIVATE_KEY");
            var aliceKeypair = new KeyPair(alicePrivateKey);
            ulong mosaicId = ulong.Parse("72C0212E67A08BCE", System.Globalization.NumberStyles.HexNumber);
            ulong amount = 10000000;
            SymbolFacade facade = new SymbolFacade(CatSdk.Symbol.Network.TestNet);

            var message = Converter.Utf8ToPlainMessage("hello symbol!");
            var networkType = NetworkType.TESTNET;
            var txs = new List<IBaseTransaction>();

            // transferTxを２つ作成(別に１つでもいい)
            txs.Add(new EmbeddedTransferTransactionV1
            {
                Network = networkType,
                SignerPublicKey = aliceKeypair.PublicKey,
                RecipientAddress = new UnresolvedAddress(Converter.StringToAddress("ALICE_ADDRESS")),
                Mosaics = new UnresolvedMosaic[]
                {
                    new()
                    {
                        MosaicId = new UnresolvedMosaicId(mosaicId),
                        Amount = new Amount(100)
                    }
                },
                Message = message
            });
            txs.Add(new EmbeddedTransferTransactionV1
            {
                Network = networkType,
                SignerPublicKey = aliceKeypair.PublicKey,
                RecipientAddress = new UnresolvedAddress(Converter.StringToAddress("ALICE_ADDRESS")),
                Mosaics = new UnresolvedMosaic[]
                {
                    new()
                    {
                        MosaicId = new UnresolvedMosaicId(mosaicId),
                        Amount = new Amount(100)
                    }
                },
                Message = message
            });

            var merkleHash = SymbolFacade.HashEmbeddedTransactions(txs.ToArray());

            var aggTx = new AggregateBondedTransactionV2
            {
                Network = NetworkType.TESTNET,
                SignerPublicKey = aliceKeypair.PublicKey,
                Fee = new Amount(1000000),
                TransactionsHash = merkleHash,
                Transactions = txs.ToArray(),
                Deadline = new Timestamp(facade.Network.FromDatetime<NetworkTimestamp>(DateTime.UtcNow).AddHours(2).Timestamp),
            };

            var aggTxHash = facade.HashTransaction(aggTx);
            // アグリゲートボンデットTxに署名
            var signature = facade.SignTransaction(aliceKeypair, aggTx);
            // アグリゲートボンデットのペイロード
            var aggTxPayload = TransactionsFactory.AttachSignature(aggTx, signature);

            // ハッシュロックTx作成
            var hashLockTx = new HashLockTransactionV1
            {
                Network = NetworkType.TESTNET,
                SignerPublicKey = aliceKeypair.PublicKey,
                Duration = new BlockDuration(5760),
                Hash = aggTxHash,
                Deadline = new Timestamp(facade.Network.FromDatetime<NetworkTimestamp>(DateTime.UtcNow).AddHours(2).Timestamp),
                Mosaic = new UnresolvedMosaic
                {
                    MosaicId = new UnresolvedMosaicId(mosaicId),
                    Amount = new Amount(amount)
                },
            };

            // ハッシュロックの手数料
            long hashLockFee = hashLockTx.Size * 200;
            hashLockTx.Fee = new Amount((ulong)hashLockFee);

            // ハッシュロックに署名
            var hashLockSignature = facade.SignTransaction(aliceKeypair, hashLockTx);
            // 署名をTxにセット
            hashLockTx.Signature = hashLockSignature;
            // ペイロード作成
            var hashLockPayload = TransactionsFactory.AttachSignature(hashLockTx, hashLockSignature);
            // ハッシュロックのハッシュ生成
            var hashLockHash = facade.HashTransaction(hashLockTx);

            // ハッシュロックアナウンス
            using var httpClient = new HttpClient();
            var hashLockContent = new StringContent(hashLockPayload, Encoding.UTF8, "application/json");
            var response = await httpClient.PutAsync(node + "/transactions", hashLockContent);

            HttpResponseMessage txStatusResponse = null;

            // 最大10分間待つ
            DateTime endTime = DateTime.UtcNow.AddMinutes(10);

            // ハッシュロックがconfirmedになるまで待つ
            while (endTime > DateTime.UtcNow)
            {
                txStatusResponse = await httpClient.GetAsync(node + $"/transactionStatus/{hashLockHash.ToString()}");

                if (txStatusResponse.IsSuccessStatusCode)
                {
                    string responseBody = await txStatusResponse.Content.ReadAsStringAsync();
                    var res = JsonConvert.DeserializeObject<TransactionStatusResponse>(responseBody);
                    Console.WriteLine(responseBody);
                    if (string.Equals(res.Group, "confirmed"))
                    {
                        break;
                    }
                }
                await Task.Delay(1000);
            }

            // アグボンアナウンス
            var content = new StringContent(aggTxPayload, Encoding.UTF8, "application/json");
            response = await httpClient.PutAsync(node + "/transactions/partial", content);
            if (response.IsSuccessStatusCode)
            {
                var responseDetailsJson = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseDetailsJson);
            }

            txStatusResponse = await httpClient.GetAsync(node + $"/transactionStatus/{aggTxHash.ToString()}");
            if (txStatusResponse.IsSuccessStatusCode)
            {
                var responseBody = await txStatusResponse.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);
                return;
            }
        }
    }
}
