﻿using System;
using System.Timers;

namespace Treachery.Test;

internal class TimedTest : IDisposable
{
    public event EventHandler<ElapsedEventArgs> Elapsed;
    private readonly Timer _timer;
    private readonly object _toTest;

    public TimedTest(object toTest, int seconds)
    {
        _toTest = toTest;
        _timer = new Timer(1000 * seconds)
        {
            AutoReset = false
        };
        _timer.Elapsed += Timer_Elapsed;
        //Console.WriteLine(DateTime.Now.ToLongTimeString() + ";Starting timer for game;" + ((Game)toTest).Seed);
        _timer.Start();
    }

    public void Stop()
    {
        //Console.WriteLine(DateTime.Now.ToLongTimeString() + ";Stopping timer for game;" + ((Game)_toTest).Seed);
        _timer.Stop();
    }

    private void Timer_Elapsed(object sender, ElapsedEventArgs e)
    {
        //Console.WriteLine(DateTime.Now.ToLongTimeString() + ";Elapsing timer for game;" + ((Game)_toTest).Seed);
        Elapsed?.Invoke(_toTest, e);
    }

    public void Dispose()
    {
        Stop();
    }
}