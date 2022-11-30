using Nethereum.Web3;
using System.Threading.Tasks;
using Nethereum.Contracts;
using System.Numerics;
using System;
using TradingBot.Functions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Newtonsoft.Json;
using Nethereum.JsonRpc.WebSocketClient;

namespace TradingBot.Utils
{


    public static class WebUtils
    {
        public static BigInteger MaxApproveAmmount = BigInteger.Parse("115792089237316195423570985008687907853269984665637437355138620109780");
        public static async Task<bool> CheckAllowance(Web3 web3, Contract tokenContract, string spender, string owner, BigInteger value = default)
        {
            if(value == default || value == null || value == 0){
                value = 0;
            }
            var func_allowance = web3.Eth.GetContractQueryHandler<GetAllowance>();
            BigInteger result = await func_allowance.QueryAsync<BigInteger>(tokenContract.Address, new GetAllowance() { Owner = owner, Spender = spender });
            return result > value;
        }

        public static async Task<string> Approve(Contract tokenContract, string spender, string from, HexBigInteger gasPrice, BigInteger? amount = null)
        {
            var func_approve = tokenContract.GetFunction("approve");

            if (amount == null)
            {
                amount = BigInteger.Parse("115792089237316195423570985008687907853269984665637437355138620109780");
            }

            object[] paramters = new object[] {
                spender,
                amount
            };

            return await func_approve.SendTransactionAsync(from: from, gas: new HexBigInteger(85000), gasPrice: gasPrice, value: new HexBigInteger(0), functionInput: paramters);
        }

        public static async Task<Nethereum.RPC.Eth.DTOs.TransactionReceipt> ApproveAndWaitReceipt(Contract tokenContract, string spender, string from, HexBigInteger gasPrice, BigInteger? amount = null)
        {
            var func_approve = tokenContract.GetFunction("approve");

            if (amount == null)
            {
                amount = BigInteger.Parse("115792089237316195423570985008687907853269984665637437355138620109780");
            }

            object[] paramters = new object[] {
                spender,
                amount
            };

            return await func_approve.SendTransactionAndWaitForReceiptAsync(from: from, gas: new HexBigInteger(85000), gasPrice: gasPrice, value: new HexBigInteger(0), functionInput: paramters);
        }


        public static async Task<BigInteger> GetAccountBalance(Web3 web3, string address)
        {
            return (await web3.Eth.GetBalance.SendRequestAsync(address)).Value;
        }

        public static async Task<BigInteger> GetBlockNumberAsync(Web3 web3)
        {
            return (await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
        }

        public static async Task NewBlockHeader_With_Observable_Subscription()
        {
            using (var client = new StreamingWebSocketClient("ws://127.0.0.1:8546"))
            {
                // create the subscription
                // (it won't start receiving data until Subscribe is called)
                var subscription = new EthNewBlockHeadersObservableSubscription(client);

                // attach a handler for when the subscription is first created (optional)
                // this will occur once after Subscribe has been called
                subscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionId =>
                    Console.WriteLine("Block Header subscription Id: " + subscriptionId));

                DateTime? lastBlockNotification = null;
                double secondsSinceLastBlock = 0;

                // attach a handler for each block
                // put your logic here
                subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(block =>
                {
                    secondsSinceLastBlock = (lastBlockNotification == null) ? 0 : (int)DateTime.Now.Subtract(lastBlockNotification.Value).TotalSeconds;
                    lastBlockNotification = DateTime.Now;
                    var utcTimestamp = DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value);
                    Console.WriteLine($"New Block. Number: {block.Number.Value}, Timestamp UTC: {JsonConvert.SerializeObject(utcTimestamp)}, Seconds since last block received: {secondsSinceLastBlock} ");
                });

                bool subscribed = true;

                // handle unsubscription
                // optional - but may be important depending on your use case
                subscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
                {
                    subscribed = false;
                    Console.WriteLine("Block Header unsubscribe result: " + response);
                });

                // open the websocket connection
                await client.StartAsync();

                // start the subscription
                // this will only block long enough to register the subscription with the client
                // once running - it won't block whilst waiting for blocks
                // blocks will be delivered to our handler on another thread
                await subscription.SubscribeAsync();

                // run for a minute before unsubscribing
                await Task.Delay(TimeSpan.FromSeconds(30));

                // unsubscribe
                await subscription.UnsubscribeAsync();

                //allow time to unsubscribe
                while (subscribed) await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        public static async Task NewPendingTransactions()
        {
            using (var client = new StreamingWebSocketClient("ws://127.0.0.1:8546"))
            {
                // create the subscription
                // it won't start receiving data until Subscribe is called on it
                var subscription = new EthNewPendingTransactionObservableSubscription(client);

                // attach a handler subscription created event (optional)
                // this will only occur once when Subscribe has been called
                subscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionId =>
                    Console.WriteLine("Pending transactions subscription Id: " + subscriptionId));

                // attach a handler for each pending transaction
                // put your logic here
                subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(transactionHash =>
                {
                    var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    Console.WriteLine(time + " - " + " : " + transactionHash);
                });

                bool subscribed = true;

                //handle unsubscription
                //optional - but may be important depending on your use case
                subscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
                {
                    subscribed = false;
                    Console.WriteLine("Pending transactions unsubscribe result: " + response);
                });

                //open the websocket connection
                await client.StartAsync();

                // start listening for pending transactions
                // this will only block long enough to register the subscription with the client
                // it won't block whilst waiting for transactions
                // transactions will be delivered to our handlers on another thread
                await subscription.SubscribeAsync();

                // run for minute
                // transactions should appear on another thread
                await Task.Delay(TimeSpan.FromSeconds(30));

                // unsubscribe
                await subscription.UnsubscribeAsync();

                // wait for unsubscribe 
                while (subscribed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }
    }
}
