using System;

namespace Bot.NamebaseClient.Responses
{
    public class ErrorResponse
    {
        public enum SuggestedRecovery
        {
            Retry,
            SyncTime,
            Fatal,
            Ignore,
            OutOfMoney,
        }

        public string Code { get; set; }
        public string Message { get; set; }

        public SuggestedRecovery HowRecovery()
        {
            switch (Code)
            {
                case "SERVER_UNKNOWN":
                    return SuggestedRecovery.Retry;
                case "SERVER_FUTURE_TIMESTAMP":
                case "SERVER_LATE_TIMESTAMP":
                    return SuggestedRecovery.SyncTime;
                case "REQUEST_UNAUTHENCIATED": // original is typo
                case "REQUEST_UNAUTHENTICATED":
                case "REQUEST_PARSE_ERROR":
                case "REQUEST_MISSING_PARAMETER":
                case "REQUEST_BAD_PARAMETER":
                case "REQUEST_EXTRA_PARAMETER":
                case "REQUEST_UNSUPPORTED_SYMBOL":
                case "REQUEST_UNSUPPORTED_ASSET":
                case "REQUEST_UNSUPPORTED_LIMIT":
                case "REQUEST_MINIMUM_WITHDRAWAL":
                case "REQUEST_MINIMUM_DEPOSIT":
                    return SuggestedRecovery.Fatal;
                case "REQUEST_MINIMUM_ORDER":
                    return SuggestedRecovery.OutOfMoney;
                case "NOT_ALLOWED_TO_TRADE":
                case "NOT_ALLOWED_TO_DEPOSIT":
                case "NOT_ALLOWED_TO_WITHDRAW":
                case "WITHDRAWAL_ADDRESS_INVALID":
                case "WITHDRAWAL_LIMIT_REACHED":
                case "WITHDRAWALS_DISABLED":
                    return SuggestedRecovery.Ignore;
                case "INSUFFICIENT_BALANCE":
                    return SuggestedRecovery.OutOfMoney;
                case "CANCEL_INVALID":
                case "CANCEL_UNAUTHORIZED":
                case "CANCEL_UNKNOWN":
                case "GET_TRADES_UNAUTHORIZED":
                    return SuggestedRecovery.Ignore;
                case "GET_ORDER_FAILED":
                    return SuggestedRecovery.Retry;
                default:
                    return SuggestedRecovery.Ignore;
            }
        }

        public class FatalError : Exception
        {
        }

        public class TimeChanged : Exception
        {
        }

        public class IgnoreFailure : Exception
        {
        }

        public class OutOfMoney : Exception
        {
            
        }
    }
}