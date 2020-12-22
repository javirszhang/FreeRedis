﻿using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

public class SingleLinks7<T>
{
    public static int Increment;
    public int Index;
    public bool InReading;
    public readonly TaskCompletionSource<T>[] Buffer;
    public SingleLinks7(int length)
    {
        Buffer = new TaskCompletionSource<T>[length];
        Index = Interlocked.Increment(ref Increment);
    }

    public SingleLinks7<T> Next;

    public SingleLinks7<T> AppendNew(int length)
    {

        var temp = new SingleLinks7<T>(length);
        temp.Next = Next;
        Next = temp;
        return temp;

    }
}

public class CircleTaskBuffer5<T> where T : new()
{
    public void Clear()
    {
        _debug.Clear();
    }
    private readonly DebugBuffer<T> _debug;
    public int ArrayLength = 8192;
    private SingleLinks7<T> _writePtr;
    public SingleLinks7<T> _readPtr;
    private TaskCompletionSource<T>[] _currentWrite;
    private TaskCompletionSource<T>[] _currentRead;
    private readonly PipeWriter _writer;
    public CircleTaskBuffer5()
    {
        //_writer = writer;
        _debug = new();
        var first = new SingleLinks7<T>(ArrayLength);
        first.InReading = true;
        first.Next = first;
        var head = first;
        for (int i = 0; i < 2; i++)
        {
            head = head.AppendNew(ArrayLength);
            //Parallel.For(0, ArrayLength, (index) => { buffer[index] = new TaskCompletionSource<T>(); });
        }

        _writePtr = first;
        _readPtr = first;
        _currentWrite = first.Buffer;
        _currentRead = first.Buffer;
        //Parallel.For(0, ArrayLength, (index) => { _currentWrite[index] = new TaskCompletionSource<T>(); });
        System.Console.WriteLine($"总环数{SingleLinks7<T>.Increment}！");
    }

    private int _write_offset;
    private int _read_offset;
    public TaskCompletionSource<T> WriteNext()
    {
        _debug.RecodSender();
        var result = new TaskCompletionSource<T>();
        _currentWrite[_write_offset] = result;
       //result.Reset();
       _write_offset += 1;
        if (_write_offset == ArrayLength)
        {
            AddBuffer();
        }
        return result;
    }

    private int _lock;
    private void AddBuffer()
    {

        SpinWait wait = default;
        while (Interlocked.CompareExchange(ref _lock, 1, 0) != 0)
        {
            _debug.RecodLock();
            wait.SpinOnce();
        }
        System.Console.Write($"环{_writePtr.Index}已满！");
        if (_writePtr.Next.InReading)
        {
            _debug.RecodContact(true);
            _writePtr = _writePtr.AppendNew(ArrayLength);
            _lock = 0;
            _currentWrite = _writePtr.Buffer;
            System.Console.WriteLine($"开辟新环，总环数{SingleLinks7<T>.Increment}！");
            Parallel.For(0, ArrayLength, (index) => { _currentWrite[index] = new TaskCompletionSource<T>(); });
        }
        else
        {
            _debug.RecodContact(false);
            _writePtr = _writePtr.Next;
            System.Console.WriteLine($"移动到环{_writePtr.Index}");
            _lock = 0;
            _currentWrite = _writePtr.Buffer;
        }
        _write_offset = 0;

    }


    public void ReadNext(T value)
    {
       
        var result = _currentRead[_read_offset];
        _debug.RecodReceiver();
        _debug.AcceptTask(result.Task);
        if (result.Task.IsCompleted)
        {
            //System.Console.WriteLine("Need False!");
            //result.SetResult((T)(object)false);
        }
        else
        {
            result.SetResult(value);
        }
        _read_offset += 1;
        if (_read_offset == ArrayLength)
        {
            CollectBuffer();
        }

    }



    private void CollectBuffer()
    {

        SpinWait wait = default;
        while (Interlocked.CompareExchange(ref _lock, 1, 0) != 0)
        {
            _debug.RecodLock();
            wait.SpinOnce();
        }
        System.Console.Write($"环{_readPtr.Index}已处理完！");
        _readPtr.InReading = false;
        _readPtr = _readPtr.Next;
        System.Console.WriteLine($"移动到环{_readPtr.Index}");
        _readPtr.InReading = true;
        _lock = 0;
        _currentRead = _readPtr.Buffer;
        _read_offset = 0;
    }
}

#if DEBUG
public class DebugBuffer<T>
{


    public void Clear()
    {
        ShowInfo();
        _senderCount = 0;
        _receiverCount = 0;
        _sendOverflowCount = 0;
        _hasCompletedCount = 0;
        _contactCount.Clear();
    }

    public static int TimeInteval = 3000;

    public DebugBuffer()
    {
        _contactCount = new List<(int oldLength, bool isNew)>();
        Task.Run(() =>
        {

            while (true)
            {
                Thread.Sleep(TimeInteval);
                ShowInfo();
            }

        });
    }


    private int _senderCount;
    public void RecodSender()
    {
        Interlocked.Increment(ref _senderCount);
    }


    private int _receiverCount;
    public void RecodReceiver()
    {
        Interlocked.Increment(ref _receiverCount);
    }


    private int _sendOverflowCount;
    public void RecodSendOverflow()
    {
        Interlocked.Increment(ref _sendOverflowCount);
    }


    private List<(int oldLength, bool isNew)> _contactCount;
    public void RecodContact(bool isNew = false)
    {
        _contactCount.Add((_senderCount, isNew));
    }


    private int _lockCount;
    public void RecodLock()
    {
        Interlocked.Increment(ref _lockCount);
    }


    private int _hasCompletedCount;

    public void AcceptTask(ValueTask<T> task)
    {
        if (task.IsCompleted)
        {
            Interlocked.Increment(ref _hasCompletedCount);
        }
    }

    public void AcceptTask(Task<T> task)
    {
        if (task.IsCompleted)
        {
            Interlocked.Increment(ref _hasCompletedCount);
        }
    }



    public void ShowInfo()
    {
        return;
        System.Console.WriteLine("-------------------------------------------------------");
        System.Console.WriteLine($"缓冲区经历了{_contactCount.Count}次移动！");
        for (int i = 0; i < _contactCount.Count; i++)
        {
            var item = _contactCount[i];
            System.Console.Write($"第{i}次移动时，发送任务{item.oldLength}个！");
            if (item.isNew)
            {
                System.Console.WriteLine($"本次进行了扩容处理！");
            }
            System.Console.WriteLine();
        }

        System.Console.WriteLine(@$"
发送任务 次数: {_senderCount}
分配结果 次数: {_receiverCount}
提前任务 异常次数:{_hasCompletedCount}
扩 容 锁 争用次数:{_lockCount}
");

        System.Console.WriteLine("-------------------------------------------------------");
        System.Console.ReadKey();
    }


}
#endif
