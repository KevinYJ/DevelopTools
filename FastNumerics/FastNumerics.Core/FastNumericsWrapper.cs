using System.Runtime.InteropServices;

namespace FastNumerics.Core;

/// <summary>
/// High-throughput array type conversion via FastNumerics.dll (NASM AVX2 x64).
///
/// Element count is derived automatically from src.Length; callers are
/// responsible for ensuring dst is pre-allocated to the correct size.
///
/// isLittleEndian parameter (functions 1-4):
///   true  = source bytes are little-endian (native x64 / most files)
///   false = source bytes are big-endian (network / big-endian binary data)
/// </summary>
public static class FastNumericsWrapper
{
    private const string DllName = "FastNumerics.dll";

    // ---- Public API (count derived from src.Length) -------------------------

    /// <summary>
    /// Interprets each 4-byte group in <paramref name="src"/> as an int32,
    /// converts the integer value to float, and writes to <paramref name="dst"/>.
    /// Requires dst.Length >= src.Length / 4.
    /// </summary>
    public static int ConvertInt32BytesToFloats(byte[] src, float[] dst, bool isLittleEndian) =>
        NativeConvertInt32BytesToFloats(src, dst, src.Length / 4, isLittleEndian ? 1 : 0);

    /// <summary>
    /// Interprets each 4-byte group in <paramref name="src"/> as an int32
    /// (with byte-order correction) and writes it to <paramref name="dst"/>.
    /// Requires dst.Length >= src.Length / 4.
    /// </summary>
    public static int ConvertInt32BytesToInt32s(byte[] src, int[] dst, bool isLittleEndian) =>
        NativeConvertInt32BytesToInt32s(src, dst, src.Length / 4, isLittleEndian ? 1 : 0);

    /// <summary>
    /// Interprets each 4-byte group in <paramref name="src"/> as an IEEE 754
    /// float, truncates towards zero to int32, and writes to <paramref name="dst"/>.
    /// Requires dst.Length >= src.Length / 4.
    /// </summary>
    public static int ConvertFloatBytesToInt32s(byte[] src, int[] dst, bool isLittleEndian) =>
        NativeConvertFloatBytesToInt32s(src, dst, src.Length / 4, isLittleEndian ? 1 : 0);

    /// <summary>
    /// Reinterprets each 4-byte group in <paramref name="src"/> as an IEEE 754
    /// float (with byte-order correction) and writes to <paramref name="dst"/>.
    /// Requires dst.Length >= src.Length / 4.
    /// </summary>
    public static int ConvertFloatBytesToFloats(byte[] src, float[] dst, bool isLittleEndian) =>
        NativeConvertFloatBytesToFloats(src, dst, src.Length / 4, isLittleEndian ? 1 : 0);

    /// <summary>
    /// Converts each int32 element to its float equivalent (round-to-nearest).
    /// Requires dst.Length >= src.Length.
    /// </summary>
    public static int ConvertInt32sToFloats(int[] src, float[] dst) =>
        NativeConvertInt32sToFloats(src, dst, src.Length);

    /// <summary>
    /// Truncates each float element towards zero to int32
    /// (equivalent to C cast <c>(int)f</c>).
    /// Requires dst.Length >= src.Length.
    /// </summary>
    public static int ConvertFloatsToInt32s(float[] src, int[] dst) =>
        NativeConvertFloatsToInt32s(src, dst, src.Length);

    // ---- Private P/Invoke bindings (count passed explicitly to ASM) ---------
    // count is required at the ASM level: the function receives only a raw
    // pointer and has no other way to determine the array boundary.

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "ConvertInt32BytesToFloats")]
    private static extern int NativeConvertInt32BytesToFloats(
        byte[] src, float[] dst, int count, int isLittleEndian);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "ConvertInt32BytesToInt32s")]
    private static extern int NativeConvertInt32BytesToInt32s(
        byte[] src, int[] dst, int count, int isLittleEndian);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "ConvertFloatBytesToInt32s")]
    private static extern int NativeConvertFloatBytesToInt32s(
        byte[] src, int[] dst, int count, int isLittleEndian);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "ConvertFloatBytesToFloats")]
    private static extern int NativeConvertFloatBytesToFloats(
        byte[] src, float[] dst, int count, int isLittleEndian);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "ConvertInt32sToFloats")]
    private static extern int NativeConvertInt32sToFloats(
        int[] src, float[] dst, int count);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "ConvertFloatsToInt32s")]
    private static extern int NativeConvertFloatsToInt32s(
        float[] src, int[] dst, int count);
}
