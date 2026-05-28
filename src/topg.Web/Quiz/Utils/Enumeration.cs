using System.Reflection;

namespace topg.Web.Quiz.Utils;

public abstract record Enumeration<T>(string Id, string DisplayName)
    where T : Enumeration<T>
{
    private static readonly Lazy<Dictionary<string, T>> AllItems;

    static Enumeration()
    {
        AllItems = new Lazy<Dictionary<string, T>>(() =>
        {
            return typeof(T)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(x => x.FieldType == typeof(T))
                .Select(x => x.GetValue(null))
                .Cast<T>()
                .ToDictionary(x => x.Id, x => x);
        });
    }

    public override string ToString() => DisplayName;

    public static IEnumerable<T> GetAll()
    {
        return AllItems.Value.Values;
    }

    public static T Parse(string value)
    {
        return AllItems.Value.TryGetValue(value, out var matchingItem) 
            ? matchingItem 
            : throw new InvalidOperationException($"'{value}' is not a valid value in {typeof(T)}");
    }
}