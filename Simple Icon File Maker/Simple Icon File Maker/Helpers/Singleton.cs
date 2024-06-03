using System;
using System.Collections.Concurrent;

namespace Simple_Icon_File_Maker;

internal static class Singleton<T> where T : new()
{
    private readonly static ConcurrentDictionary<Type, T> _instances = new();

    public static T Instance
    {
        get
        {
            return _instances.GetOrAdd(typeof(T), (t) => new T());
        }
    }
}
