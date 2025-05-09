namespace ImageApi.Helpers
{
    public static class ApiKeyStore
    {
        private static IDictionary<string, string>? _keyToPlan;
        private static IDictionary<string, PlanLimit>? _planConfig;

        public static void Initialize(IConfiguration config)
        {
            _keyToPlan = config.GetSection("ApiKeyPlans")
                               .Get<Dictionary<string, string>>()
                           ?? new Dictionary<string, string>();

            _planConfig = config.GetSection("PlanConfig")
                                .Get<Dictionary<string, PlanLimit>>()
                          ?? new Dictionary<string, PlanLimit>();
        }

        public static PlanLimit GetLimitsForKey(string apiKey)
        {
            if (_keyToPlan != null && _keyToPlan.TryGetValue(apiKey, out var plan)
                && _planConfig != null && _planConfig.TryGetValue(plan, out var limits))
            {
                return limits;
            }
            // fallback to Free if unknown
            return _planConfig?["Free"]
                   ?? new PlanLimit { RequestsPerMinute = 30, DailyLimit = 500 };
        }
    }

    public class PlanLimit
    {
        public int RequestsPerMinute { get; set; }
        public int DailyLimit { get; set; }
    }
}