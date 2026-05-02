using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FastNumerics.Core;

// ============================================================
// FastNumerics Performance Benchmarks
//
// Compares FastNumerics (AVX2 + 4x unroll + prefetchnta) against
// two pure-C# baselines for every exported function:
//   "C# loop"   - plain for-loop (typical application code)
//   "C# unsafe" - unsafe pointer loop (no bounds checks)
//
// Run in Release mode for representative numbers:
//   dotnet run -c Release
// ============================================================

const int N = 10_000_000;   // elements per test
const int Warmup = 3;            // JIT + cache warmup rounds
const int Runs = 7;            // timed rounds; best-of-N reported

var sw = new Stopwatch();
var rng = new Random(42);

Console.WriteLine("=================================================================");
Console.WriteLine("  FastNumerics Performance Benchmarks");
Console.WriteLine($"  Elements : {N:N0}  ({N * 4L / 1024 / 1024} MB per array)");
Console.WriteLine($"  Warmup   : {Warmup}  Timed: {Runs} rounds (best of {Runs})");
Console.WriteLine("=================================================================");
Console.WriteLine();

// ---- shared buffers -----------------------------------------------
float[] dstFloats = new float[N];
int[] dstInts = new int[N];
int[] srcInts = new int[N];
float[] srcFloats = new float[N];
byte[] srcBytesLE = new byte[N * 4];   // LE int32 bytes
byte[] srcBytesBE = new byte[N * 4];   // BE int32 bytes
byte[] srcFloatBytesLE = new byte[N * 4];  // LE float bytes
byte[] srcFloatBytesBE = new byte[N * 4];  // BE float bytes

for (int i = 0; i < N; i++)
{
    int v = i + 1;
    BinaryPrimitives.WriteInt32LittleEndian(srcBytesLE.AsSpan(i * 4), v);
    BinaryPrimitives.WriteInt32BigEndian(srcBytesBE.AsSpan(i * 4), v);
    srcInts[i] = v;
    srcFloats[i] = (float)v + (float)rng.NextDouble();
    BinaryPrimitives.WriteSingleLittleEndian(srcFloatBytesLE.AsSpan(i * 4), srcFloats[i]);
    BinaryPrimitives.WriteSingleBigEndian(srcFloatBytesBE.AsSpan(i * 4), srcFloats[i]);
}

// ====================================================================
// 1. ConvertInt32BytesToFloats
// ====================================================================
double baseline = 0;
NewSection("ConvertInt32BytesToFloats  (int32 bytes -> float[])");

Bench("FastNumerics  LE (AVX2)",
    () => FastNumericsWrapper.ConvertInt32BytesToFloats(srcBytesLE, dstFloats, isLittleEndian: true));

Bench("FastNumerics  BE (AVX2)",
    () => FastNumericsWrapper.ConvertInt32BytesToFloats(srcBytesBE, dstFloats, isLittleEndian: false));

Bench("C# loop  LE  [baseline]",
    () => { for (int i = 0; i < N; i++) dstFloats[i] = (float)BitConverter.ToInt32(srcBytesLE, i * 4); },
    setBaseline: true);

Bench("C# loop  BE",
    () => { for (int i = 0; i < N; i++) dstFloats[i] = (float)BinaryPrimitives.ReadInt32BigEndian(srcBytesBE.AsSpan(i * 4)); });

unsafe
{
    Bench("C# unsafe LE",
        () => { fixed (byte* p = srcBytesLE) { int* ip = (int*)p; for (int i = 0; i < N; i++) dstFloats[i] = (float)ip[i]; } });
}

// ====================================================================
// 2. ConvertInt32BytesToInt32s
// ====================================================================
NewSection("ConvertInt32BytesToInt32s  (int32 bytes -> int32[])");

Bench("FastNumerics  LE (AVX2)",
    () => FastNumericsWrapper.ConvertInt32BytesToInt32s(srcBytesLE, dstInts, isLittleEndian: true));

Bench("FastNumerics  BE (AVX2)",
    () => FastNumericsWrapper.ConvertInt32BytesToInt32s(srcBytesBE, dstInts, isLittleEndian: false));

Bench("Int flex buffer",
    () => { var flex = new FlexInt32sBuffer(srcBytesLE); for (int i = 0; i < N; i++) dstInts[i] = flex[i]; });

Bench("C# Span.Cast LE  [baseline]",
    () => MemoryMarshal.Cast<byte, int>(srcBytesLE).CopyTo(dstInts),
    setBaseline: true);

Bench("C# loop  BE",
    () => { for (int i = 0; i < N; i++) dstInts[i] = BinaryPrimitives.ReadInt32BigEndian(srcBytesBE.AsSpan(i * 4)); });

unsafe
{
    Bench("C# unsafe LE (memcpy)",
        () => { fixed (byte* p = srcBytesLE) fixed (int* d = dstInts) Buffer.MemoryCopy(p, d, (long)N * 4, (long)N * 4); });
}

// ====================================================================
// 3. ConvertFloatBytesToInt32s
// ====================================================================
NewSection("ConvertFloatBytesToInt32s  (float bytes -> int32[])");

Bench("FastNumerics  LE (AVX2)",
    () => FastNumericsWrapper.ConvertFloatBytesToInt32s(srcFloatBytesLE, dstInts, isLittleEndian: true));

Bench("FastNumerics  BE (AVX2)",
    () => FastNumericsWrapper.ConvertFloatBytesToInt32s(srcFloatBytesBE, dstInts, isLittleEndian: false));

Bench("C# loop  LE  [baseline]",
    () => { for (int i = 0; i < N; i++) dstInts[i] = (int)BitConverter.ToSingle(srcFloatBytesLE, i * 4); },
    setBaseline: true);

Bench("C# loop  BE",
    () => { for (int i = 0; i < N; i++) dstInts[i] = (int)BinaryPrimitives.ReadSingleBigEndian(srcFloatBytesBE.AsSpan(i * 4)); });

unsafe
{
    Bench("C# unsafe LE",
        () => { fixed (byte* p = srcFloatBytesLE) { float* fp = (float*)p; for (int i = 0; i < N; i++) dstInts[i] = (int)fp[i]; } });
}

// ====================================================================
// 4. ConvertFloatBytesToFloats
// ====================================================================
NewSection("ConvertFloatBytesToFloats  (float bytes -> float[])");

Bench("FastNumerics  LE (AVX2)",
    () => FastNumericsWrapper.ConvertFloatBytesToFloats(srcFloatBytesLE, dstFloats, isLittleEndian: true));

Bench("FastNumerics  BE (AVX2)",
    () => FastNumericsWrapper.ConvertFloatBytesToFloats(srcFloatBytesBE, dstFloats, isLittleEndian: false));

Bench("float flex buffer",
    () => { var flex = new FlexFloatsBuffer(srcFloatBytesLE); for (int i = 0; i < N; i++) dstFloats[i] = flex[i]; });

Bench("C# Span.Cast LE  [baseline]",
    () => MemoryMarshal.Cast<byte, float>(srcFloatBytesLE).CopyTo(dstFloats),
    setBaseline: true);

Bench("C# loop  BE",
    () => { for (int i = 0; i < N; i++) dstFloats[i] = BinaryPrimitives.ReadSingleBigEndian(srcFloatBytesBE.AsSpan(i * 4)); });

unsafe
{
    Bench("C# unsafe LE (memcpy)",
        () => { fixed (byte* p = srcFloatBytesLE) fixed (float* d = dstFloats) Buffer.MemoryCopy(p, d, (long)N * 4, (long)N * 4); });
}

// ====================================================================
// 5. ConvertInt32sToFloats
// ====================================================================
NewSection("ConvertInt32sToFloats  (int32[] -> float[])");

Bench("FastNumerics  (AVX2)",
    () => FastNumericsWrapper.ConvertInt32sToFloats(srcInts, dstFloats));

Bench("C# loop  [baseline]",
    () => { for (int i = 0; i < N; i++) dstFloats[i] = (float)srcInts[i]; },
    setBaseline: true);

unsafe
{
    Bench("C# unsafe",
        () => { fixed (int* sp = srcInts) fixed (float* dp = dstFloats) for (int i = 0; i < N; i++) dp[i] = (float)sp[i]; });
}

// ====================================================================
// 6. ConvertFloatsToInt32s
// ====================================================================
NewSection("ConvertFloatsToInt32s  (float[] -> int32[])");

Bench("FastNumerics  (AVX2)",
    () => FastNumericsWrapper.ConvertFloatsToInt32s(srcFloats, dstInts));

Bench("C# loop  [baseline]",
    () => { for (int i = 0; i < N; i++) dstInts[i] = (int)srcFloats[i]; },
    setBaseline: true);

unsafe
{
    Bench("C# unsafe",
        () => { fixed (float* sp = srcFloats) fixed (int* dp = dstInts) for (int i = 0; i < N; i++) dp[i] = (int)sp[i]; });
}

Console.WriteLine();
Console.WriteLine("Done.");
if (Console.IsInputRedirected is false)
{
    Console.Write("Press Enter to exit...");
    Console.ReadLine();
}

// ====================================================================
// Helpers
// ====================================================================

void NewSection(string title)
{
    baseline = 0;
    Console.WriteLine($"--- {title} ---");
    Console.WriteLine($"  {"Implementation",-30} {"Best ms",8}  {"Throughput",12}  {"vs baseline",12}");
    Console.WriteLine($"  {new string('-', 30)} {new string('-', 8)}  {new string('-', 12)}  {new string('-', 12)}");
}

void Bench(string label, Action action, bool setBaseline = false)
{
    // warmup
    for (int i = 0; i < Warmup; i++) action();

    // timed runs
    double best = double.MaxValue;
    for (int i = 0; i < Runs; i++)
    {
        sw.Restart();
        action();
        sw.Stop();
        if (sw.Elapsed.TotalMilliseconds < best)
            best = sw.Elapsed.TotalMilliseconds;
    }

    if (setBaseline) baseline = best;

    // throughput: read input (N*4 bytes) + write output (N*4 bytes)
    double mbps = (2.0 * N * 4) / (best / 1000.0) / (1024.0 * 1024.0);

    string speedup = (baseline > 0 && !setBaseline)
        ? $"x{baseline / best,6:F2}"
        : (setBaseline ? "(baseline)" : "");

    Console.WriteLine($"  {label,-30} {best,8:F1}  {mbps,10:F0} MB/s  {speedup,12}");
}

[StructLayout(LayoutKind.Explicit, Pack = 2)]
sealed class FlexFloatsBuffer
{

    [FieldOffset(0)]
    public int numberOfBytes;

    [FieldOffset(8)]
    private byte[] byteBuffer;
    [FieldOffset(8)]
    private float[] floatBuffer;

    public int Size
    {
        get { return numberOfBytes >> 2; }
    }

    public int NumberOfBytes
    {
        get { return numberOfBytes; }
    }

    public FlexFloatsBuffer(byte[] bytes)
    {
        byteBuffer = bytes;
        numberOfBytes = bytes.Length;
    }

    public FlexFloatsBuffer(float[] floats)
    {
        floatBuffer = floats;
        numberOfBytes = floats.Length * sizeof(float);
    }

    public float this[long index]
    {
        get
        {
            return floatBuffer[index];
        }

        set
        {
            floatBuffer[index] = value;
        }
    }

}

[StructLayout(LayoutKind.Explicit, Pack = 2)]
sealed class FlexInt32sBuffer
{

    [FieldOffset(0)]
    public int numberOfBytes;

    [FieldOffset(8)]
    private byte[] byteBuffer;
    [FieldOffset(8)]
    private int[] intBuffer;

    public int Size
    {
        get { return numberOfBytes >> 2; }
    }

    public int NumberOfBytes
    {
        get { return numberOfBytes; }
    }

    public FlexInt32sBuffer(byte[] bytes)
    {
        byteBuffer = bytes;
        numberOfBytes = bytes.Length;
    }

    public FlexInt32sBuffer(int[] ints)
    {
        intBuffer = ints;
        numberOfBytes = ints.Length * sizeof(Int32);
    }

    public int this[long index]
    {
        get
        {
            return intBuffer[index];
        }

        set
        {
            intBuffer[index] = value;
        }
    }

}
