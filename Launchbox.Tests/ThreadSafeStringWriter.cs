using System;
using System.IO;
using System.Text;

namespace Launchbox.Tests;

public class ThreadSafeStringWriter : StringWriter
{
    private readonly object _lock = new object();

    public override void Write(char value)
    {
        lock (_lock)
        {
            base.Write(value);
        }
    }

    public override void Write(string? value)
    {
        lock (_lock)
        {
            base.Write(value);
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        lock (_lock)
        {
            base.Write(buffer, index, count);
        }
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return base.ToString();
        }
    }
}
