namespace Redis.Helper
{
    using System.Reflection;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    internal class RedisConverter
    {
        public static HashEntry[] ToHashEntries(object obj)
        {
            PropertyInfo[] properties = obj.GetType().GetProperties();
            return properties.Where(x => x.GetValue(obj) != null)
                .Select(property =>
                {
                    object propertyValue = property.GetValue(obj);
                    string? hashValue;
                    if (propertyValue is IEnumerable<object>)
                    {
                        hashValue = JsonConvert.SerializeObject(propertyValue);
                    }
                    else
                    {
                        hashValue = Convert.ToString(propertyValue);
                    }

                    return new HashEntry(property.Name, hashValue);
                }).ToArray();
        }

        public static T ConvertFromRedis<T>(HashEntry[] hashEntries)
        {
            PropertyInfo[] properties = typeof(T).GetProperties();
            object? obj = Activator.CreateInstance(typeof(T));
            foreach (PropertyInfo property in properties)
            {
                HashEntry entry = hashEntries.FirstOrDefault(e => Convert.ToString(e.Name).Equals(property.Name));
                if (entry.Equals(default))
                {
                    continue;
                }

                property.SetValue(obj, Convert.ChangeType(Convert.ToString(entry.Value), property.PropertyType));
            }

            return (T)obj;
        }
    }
}
