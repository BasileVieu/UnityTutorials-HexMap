using System.Collections.Generic;

public static class ListPool<T>
{
    private static Stack<List<T>> s_stack = new Stack<List<T>>();

    public static List<T> Get()
    {
        if (s_stack.Count > 0)
        {
            return s_stack.Pop();
        }

        return new List<T>();
    }

    public static void Add(List<T> list)
    {
        list.Clear();

        s_stack.Push(list);
    }
}