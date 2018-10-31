using Ont.SmartContract.Framework;
using Ont.SmartContract.Framework.Services.Ont;
using Ont.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace HP
{
    public class HPToken : SmartContract
    {
        //Token Settings
        public static string Name() => "My Token";
        public static string Symbol() => "MT";
        public static readonly byte[] owner = "AGjD4Mo25kzcStyh1stp7tXkUuMopD43NT".ToScriptHash();
        public static byte Decimal() => 8;
        private const ulong factor = 100000000; //decided by Decimals()
        private const ulong totalAmount = 10000000000 * factor;

        //Store Key Prefix
        private static byte[] transferPrefix = { 0x01 };
        private static byte[] approvePrefix = { 0x02 };
        // private static byte[] totalSupply = "totalSupply".AsByteArray();
        // "totalSupply"

        public delegate void deleTransfer(byte[] from, byte[] to, BigInteger value);
        [DisplayName("transfer")]
        public static event deleTransfer Transferred;

        public delegate void deleApprove(byte[] onwer, byte[] spender, BigInteger value);
        [DisplayName("approval")]
        public static event deleApprove Approval;

        public struct State
        {
            public byte[] From;
            public byte[] To;
            public BigInteger Amount;
        }

        public static Object Main(string operation, params object[] args)
        {
            if (operation == "Init") return Init();
            if (operation == "Decimal") return Decimal();
            if (operation == "TotalSupply") return TotalSupply();
            if (operation == "Name") return Name();
            if (operation == "Symbol") return Symbol();
            if (operation == "BalanceOf")
            {
                if (args.Length != 1) return 0;
                byte[] address = (byte[])args[0];
                return BalanceOf(address);
            }
            if (operation == "Transfer")
            {
                if (args.Length != 3) return false;
                byte[] from = (byte[])args[0];
                byte[] to = (byte[])args[1];
                BigInteger value = (BigInteger)args[2];
                return Transfer(from, to, value);
            }
            if (operation == "TransferMulti")
            {
                return TransferMulti(args);
            }
            if (operation == "Approve")
            {
                if (args.Length != 3) return false;
                byte[] owner = (byte[])args[0];
                if (owner.Length != 20)
                {
                    return false;
                }
                byte[] spender = (byte[])args[1];
                if (spender.Length != 20)
                {
                    return false;
                }
                BigInteger value = (BigInteger)args[2];
                return Approve(owner, spender, value);
            }
            if (operation == "Allowance")
            {
                if (args.Length != 2) return 0;
                byte[] owner = (byte[])args[0];
                byte[] spender = (byte[])args[0];
                return Allowance(owner, spender);
            }
            if (operation == "TransferFrom")
            {
                if (args.Length != 4) return false;
                byte[] sender = (byte[])args[0];
                if (sender.Length != 20)
                {
                    return false;
                }
                byte[] from = (byte[])args[1];
                if (from.Length != 20)
                {
                    return false;
                }
                byte[] to = (byte[])args[2];
                if (to.Length != 20)
                {
                    return false;
                }
                BigInteger amount = (BigInteger)args[3];
                return TransferFrom(sender, from, to, amount);
            }
            return false;
        }

        /// <summary>initialize contract parameter</summary>
        /// <returns>initialize result, success or failure</returns>
        //[DisplayName("init")]
        public static bool Init()
        {
            byte[] total_supply = Storage.Get(Storage.CurrentContext, "totalSupply");
            if (total_supply.Length != 0) return false;

            Storage.Put(Storage.CurrentContext, owner, totalAmount);
            Runtime.Notify(null, owner, totalAmount);
            Storage.Put(Storage.CurrentContext, "totalSupply", totalAmount);
            return true;
        }

        /// <summary>query the total supply of token</summary>
        /// <returns>total supply of token </returns>
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }


        /// <summary>query balance of any address</summary>
        /// <returns>balance of the address</returns>
        /// <param name="address">account or contract address</param>
        public static BigInteger BalanceOf(byte[] address)
        {
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
        }

        /// <summary>
        ///     transfer amount of token from sender to receiver,
        ///     the address can be account or contract address which should be 20-byte
        /// </summary>
        /// <returns>transfer result, success or failure</returns>
        /// <param name="from">transfer sender address</param>
        /// <param name="to">transfer receiver address</param>
        /// <param name="value">transfer amount</param>
        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (value < 0) return false;
            if (!Runtime.CheckWitness(from)) return false;
            if (to.Length != 20) return false;

            BigInteger fromValue = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
            if (fromValue < value) return false;
            if (fromValue == value)
                Storage.Delete(Storage.CurrentContext, from);
            else
                Storage.Put(Storage.CurrentContext, from, fromValue - value);

            BigInteger toValue = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
            Storage.Put(Storage.CurrentContext, to, toValue + value);
            Runtime.Notify(from, to, value);
            return true;
        }

        /// <summary>transfer multiple amount of token from  multiple sender to multiple receiver</summary>
        /// <returns>return transfer result, if any transfer fail, all of transfers should fail. </returns>
        /// <param name="args">state struct</param>
        ///  public struct State
        /// {
        ///    public byte[] From; // transfer sender
        ///    public byte[] To; // transfer receiver
        ///    public BigInteger Amount; //transfer amount
        ///}
        public static bool TransferMulti(object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                State state = (State)args[i];
                if (!Transfer(state.From, state.To, state.Amount)) throw new Exception();
            }
            return true;
        }

        /// <summary>
        ///     approve allows spender to withdraw from owner account multiple times, up to the value amount
        ///     the address can be account or contract address which should be 20-byte
        /// </summary>
        /// <returns>transfer result, success or failure</returns>
        /// <param name="from">approve owner address</param>
        /// <param name="to">approve spender address</param>
        /// <param name="value">approve amount</param>
        public static bool Approve(byte[] owner, byte[] spender, BigInteger amount)
        {
            if (amount < 0) return false;
            if (!Runtime.CheckWitness(owner)) return false;
            if (!ValidateAddress(spender)) return false;
            if (owner == spender) return false;

            BigInteger ownerBalance = BalanceOf(owner);
            if (ownerBalance < amount) return false;

            Storage.Put(Storage.CurrentContext, approvePrefix.Concat(owner).Concat(spender), amount);

            Runtime.Notify("approve", owner, spender, amount);
            return true;
        }

        /// <summary>
        ///     transferFrom allows `spender` to withdraw amount of token from `from` account to `to` account
        ///     the address can be account or contract address which should be 20-byte
        /// </summary>
        /// <returns>transferFrom result, success or failure</returns>
        /// <param name="spender">approve owner address</param>
        /// <param name="from">approve owner address</param>
        /// <param name="to">approve spender address</param>
        /// <param name="value">approve amount</param>
        public static bool TransferFrom(byte[] spender, byte[] from, byte[] to, BigInteger amount)
        {
            if (amount < 0) return false;
            if (!Runtime.CheckWitness(spender)) return false;
            if (!ValidateAddress(from)) return false;
            if (!ValidateAddress(to)) return false;
            if (from == to) return true;
            if (BalanceOf(from) < amount) return false;

            byte[] approveKey = approvePrefix.Concat(from).Concat(spender);
            BigInteger approveValue = Storage.Get(Storage.CurrentContext, approveKey).AsBigInteger();
            if (approveValue < amount) return false;
            if (approveValue == amount)
                Storage.Delete(Storage.CurrentContext, approveKey);
            else
                Storage.Put(Storage.CurrentContext, approveKey, approveValue - amount);

            Storage.Put(Storage.CurrentContext, from, BalanceOf(from) - amount);
            Storage.Put(Storage.CurrentContext, from, BalanceOf(to) - amount);

            return Transfer(from, to, amount);
        }

        /// <summary>query `spender` can withdraw the amount of token from `owner` account </summary>
        /// <returns>withdraw amount of token</returns>
        /// <param name="owner">account or contract address</param>
        /// <param name="spender">account or contract address</param>
        public static BigInteger Allowance(byte[] owner, byte[] spender)
        {
            return Storage.Get(Storage.CurrentContext, approvePrefix.Concat(owner).Concat(spender)).AsBigInteger();
        }

        private static bool ValidateAddress(byte[] address)
        {
            if (address.Length != 20) return false;
            if (address.AsBigInteger() == 0) return false;
            return true;
        }

    }
}
