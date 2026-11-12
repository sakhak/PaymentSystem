
using kh.gov.nbc.bakong_khqr;
using kh.gov.nbc.bakong_khqr.model;
namespace PaymentSystem.Services
{
    public class KhqrService
    {
        private readonly IConfiguration _configuration;

        public KhqrService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public (string Qr, string Md5) GenerateIndividual(
            decimal amount,
            string currency,
            string orderId)
        {
            var accountId =
                _configuration["Khqr:BakongAccountId"];

            var accountName =
                _configuration["Khqr:MerchantName"];

            if (string.IsNullOrWhiteSpace(accountId) ||
                string.IsNullOrWhiteSpace(accountName))
            {
                throw new InvalidOperationException(
                    "KHQR account configuration is missing.");
            }

            var response = BakongKHQR.GenerateIndividual(
                new IndividualInfo
                {
                    BakongAccountID = accountId,
                    MerchantName = accountName,
                    MerchantCity = "PHNOM PENH",
                    Currency = currency == "USD"
                        ? KHQRCurrency.USD
                        : KHQRCurrency.KHR,
                    Amount = (double)amount,
                    BillNumber = orderId,
                    ExpirationTimestamp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()
                });

            if (response.Status.Code != 0 ||
                response.Data == null)
            {
                throw new InvalidOperationException(
                    "KHQR generation failed: " +
                    response.Status.Message);
            }

            return (
                response.Data.QR,
                response.Data.MD5
            );
        }
    }
}
