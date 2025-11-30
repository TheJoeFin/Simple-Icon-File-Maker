using System.Collections.Concurrent;

namespace Simple_Icon_File_Maker;

internal static class Singleton<T> where T : new()
{
    private static readonly ConcurrentDictionary<Type, T> _instances = new();

    public static T Instance => _instances.GetOrAdd(typeof(T), (t) => new T());
}
