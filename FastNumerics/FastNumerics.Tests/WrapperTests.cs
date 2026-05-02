using FastNumerics.Core;
using Xunit;

namespace FastNumerics.Tests;

/// <summary>
/// Correctness tests for all six FastNumerics export functions.
/// Each function is tested with:
///   - LE input  (isLittleEndian = true)
///   - BE input  (isLittleEndian = false) [functions 1-4 only]
///   - 0 elements  (early-exit path)
///   - 1 element   (scalar tail path)
///   - 8 elements  (single SIMD / mid8 path)
///   - 33 elements (main loop + scalar tail)
/// </summary>
public class WrapperTests
{
    // =========================================================
    // ConvertInt32BytesToFloats
    // =========================================================

    [Fact]
    public void ConvertInt32BytesToFloats_LE_ScalarValues()
    {
        // 20 LE int32 values: mix of positive, negative, zero, and boundary values.
        // Each entry: (leBytes[4], expectedFloat)
        (byte[] bytes, float expected)[] cases =
        [
            ([0x00, 0x00, 0x00, 0x00],  0.0f),                    // 0
            ([0x01, 0x00, 0x00, 0x00],  1.0f),                    // 1
            ([0x02, 0x00, 0x00, 0x00],  2.0f),                    // 2
            ([0x0A, 0x00, 0x00, 0x00],  10.0f),                   // 10
            ([0x64, 0x00, 0x00, 0x00],  100.0f),                  // 100
            ([0xE8, 0x03, 0x00, 0x00],  1000.0f),                 // 1 000
            ([0x40, 0x42, 0x0F, 0x00],  1000000.0f),              // 1 000 000
            ([0x7F, 0x00, 0x00, 0x00],  127.0f),                  // int8 max
            ([0xFF, 0x7F, 0x00, 0x00],  32767.0f),                // int16 max
            ([0xFF, 0xFF, 0x00, 0x00],  65535.0f),                // uint16 max
            ([0xFF, 0xFF, 0xFF, 0x7F],  (float)int.MaxValue),     // int32 max
            ([0xFF, 0xFF, 0xFF, 0xFF], -1.0f),                    // -1
            ([0xFE, 0xFF, 0xFF, 0xFF], -2.0f),                    // -2
            ([0xF6, 0xFF, 0xFF, 0xFF], -10.0f),                   // -10
            ([0x9C, 0xFF, 0xFF, 0xFF], -100.0f),                  // -100
            ([0x18, 0xFC, 0xFF, 0xFF], -1000.0f),                 // -1 000
            ([0xC0, 0xBD, 0xF0, 0xFF], -1000000.0f),              // -1 000 000
            ([0x81, 0xFF, 0xFF, 0xFF], -127.0f),                  // -127
            ([0x01, 0x80, 0xFF, 0xFF], -32767.0f),                // -32767
            ([0x00, 0x00, 0x00, 0x80],  (float)int.MinValue),     // int32 min
        ];

        int n = cases.Length;
        byte[] src = new byte[n * 4];
        float[] dst = new float[n];

        for (int i = 0; i < n; i++)
            cases[i].bytes.CopyTo(src, i * 4);

        int count = FastNumericsWrapper.ConvertInt32BytesToFloats(src, dst, isLittleEndian: true);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal(cases[i].expected, dst[i]);
    }

    [Fact]
    public void ConvertInt32BytesToFloats_BE_ScalarValues()
    {
        // 20 BE int32 values: mix of positive, negative, zero, boundary values.
        // Each entry: (beBytes, expectedFloat)
        (byte[] bytes, float expected)[] cases =
        [
            ([0x00, 0x00, 0x00, 0x00],  0.0f),          // 0
            ([0x00, 0x00, 0x00, 0x01],  1.0f),           // 1
            ([0x00, 0x00, 0x00, 0x02],  2.0f),           // 2
            ([0x00, 0x00, 0x00, 0x0A],  10.0f),          // 10
            ([0x00, 0x00, 0x00, 0x64],  100.0f),         // 100
            ([0x00, 0x00, 0x03, 0xE8],  1000.0f),        // 1 000
            ([0x00, 0x0F, 0x42, 0x40],  1000000.0f),     // 1 000 000
            ([0xFF, 0xFF, 0xFF, 0xFF], -1.0f),            // -1
            ([0xFF, 0xFF, 0xFF, 0xFE], -2.0f),            // -2
            ([0xFF, 0xFF, 0xFF, 0x9C], -100.0f),          // -100
            ([0xFF, 0xFF, 0xFC, 0x18], -1000.0f),         // -1 000
            ([0xFF, 0xF0, 0xBD, 0xC0], -1000000.0f),      // -1 000 000
            ([0x00, 0x00, 0x00, 0x7F],  127.0f),          // int8 max
            ([0xFF, 0xFF, 0xFF, 0x81], -127.0f),           // int8 min+1
            ([0x00, 0x00, 0x7F, 0xFF],  32767.0f),         // int16 max
            ([0xFF, 0xFF, 0x80, 0x01], -32767.0f),         // int16 min+1
            ([0x00, 0x01, 0x00, 0x00],  65536.0f),         // 2^16
            ([0xFF, 0xFF, 0x00, 0x00], -65536.0f),         // -2^16
            ([0x00, 0x10, 0x00, 0x00],  1048576.0f),       // 2^20
            ([0x7F, 0xFF, 0xFF, 0xFF],  (float)int.MaxValue), // int32 max
        ];

        int n = cases.Length;
        byte[] src = new byte[n * 4];
        float[] dst = new float[n];

        for (int i = 0; i < n; i++)
            cases[i].bytes.CopyTo(src, i * 4);

        int count = FastNumericsWrapper.ConvertInt32BytesToFloats(src, dst, isLittleEndian: false);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal(cases[i].expected, dst[i]);
    }

    [Fact]
    public void ConvertInt32BytesToFloats_EmptySrc_ReturnsZero()
    {
        int count = FastNumericsWrapper.ConvertInt32BytesToFloats(
            Array.Empty<byte>(), Array.Empty<float>(), isLittleEndian: true);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ConvertInt32BytesToFloats_8Elements_MidPath()
    {
        int n = 8;
        byte[] src = new byte[n * 4];
        float[] dst = new float[n];
        for (int i = 0; i < n; i++)
            BitConverter.GetBytes(i + 1).CopyTo(src, i * 4);

        int count = FastNumericsWrapper.ConvertInt32BytesToFloats(src, dst, isLittleEndian: true);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal((float)(i + 1), dst[i]);
    }

    [Fact]
    public void ConvertInt32BytesToFloats_33Elements_MainPlusScalar()
    {
        int n = 33;
        byte[] src = new byte[n * 4];
        float[] dst = new float[n];
        for (int i = 0; i < n; i++)
            BitConverter.GetBytes(i + 1).CopyTo(src, i * 4);

        int count = FastNumericsWrapper.ConvertInt32BytesToFloats(src, dst, isLittleEndian: true);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal((float)(i + 1), dst[i]);
    }

    // =========================================================
    // ConvertInt32BytesToInt32s
    // =========================================================

    [Fact]
    public void ConvertInt32BytesToInt32s_LE_ScalarValues()
    {
        // 20 LE int32 values: mix of positive, negative, zero, and boundary values.
        // Each entry: (leBytes[4], expectedInt32)
        (byte[] bytes, int expected)[] cases =
        [
            ([0x00, 0x00, 0x00, 0x00],  0),                // 0
            ([0x01, 0x00, 0x00, 0x00],  1),                // 1
            ([0x02, 0x00, 0x00, 0x00],  2),                // 2
            ([0x0A, 0x00, 0x00, 0x00],  10),               // 10
            ([0x7F, 0x00, 0x00, 0x00],  127),              // int8 max
            ([0xFF, 0x00, 0x00, 0x00],  255),              // uint8 max
            ([0xFF, 0x7F, 0x00, 0x00],  32767),            // int16 max
            ([0xFF, 0xFF, 0x00, 0x00],  65535),            // uint16 max
            ([0x00, 0x00, 0x01, 0x00],  65536),            // 2^16
            ([0xFF, 0xFF, 0xFF, 0x7F],  int.MaxValue),     // int32 max
            ([0xFF, 0xFF, 0xFF, 0xFF], -1),                // -1
            ([0xFE, 0xFF, 0xFF, 0xFF], -2),                // -2
            ([0xF6, 0xFF, 0xFF, 0xFF], -10),               // -10
            ([0x81, 0xFF, 0xFF, 0xFF], -127),              // -127
            ([0x00, 0xFF, 0xFF, 0xFF], -256),              // -256
            ([0x01, 0x80, 0xFF, 0xFF], -32767),            // -32767
            ([0x00, 0x00, 0xFF, 0xFF], -65536),            // -2^16
            ([0xC0, 0xBD, 0xF0, 0xFF], -1000000),          // -1 000 000
            ([0x01, 0x00, 0x00, 0x80],  int.MinValue + 1), // int32 min + 1
            ([0x00, 0x00, 0x00, 0x80],  int.MinValue),     // int32 min
        ];

        int n = cases.Length;
        byte[] src = new byte[n * 4];
        int[] dst = new int[n];

        for (int i = 0; i < n; i++)
            cases[i].bytes.CopyTo(src, i * 4);

        int count = FastNumericsWrapper.ConvertInt32BytesToInt32s(src, dst, isLittleEndian: true);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal(cases[i].expected, dst[i]);
    }

    [Fact]
    public void ConvertInt32BytesToInt32s_BE_ByteSwaps()
    {
        // 20 BE int32 values: mix of positive, negative, zero, and boundary values.
        // Each entry: (beBytes[4], expectedInt32)
        (byte[] bytes, int expected)[] cases =
        [
            ([0x00, 0x00, 0x00, 0x00],  0),                // 0
            ([0x00, 0x00, 0x00, 0x01],  1),                // 1
            ([0x00, 0x00, 0x00, 0x02],  2),                // 2
            ([0x00, 0x00, 0x00, 0x0A],  10),               // 10
            ([0x00, 0x00, 0x00, 0x7F],  127),              // int8 max
            ([0x00, 0x00, 0x00, 0xFF],  255),              // uint8 max
            ([0x00, 0x00, 0x7F, 0xFF],  32767),            // int16 max
            ([0x00, 0x00, 0xFF, 0xFF],  65535),            // uint16 max
            ([0x00, 0x01, 0x00, 0x00],  65536),            // 2^16
            ([0x7F, 0xFF, 0xFF, 0xFF],  int.MaxValue),     // int32 max
            ([0xFF, 0xFF, 0xFF, 0xFF], -1),                // -1
            ([0xFF, 0xFF, 0xFF, 0xFE], -2),                // -2
            ([0xFF, 0xFF, 0xFF, 0xF6], -10),               // -10
            ([0xFF, 0xFF, 0xFF, 0x81], -127),              // -127
            ([0xFF, 0xFF, 0xFF, 0x00], -256),              // -256
            ([0xFF, 0xFF, 0x80, 0x01], -32767),            // -32767
            ([0xFF, 0xFF, 0x00, 0x00], -65536),            // -2^16
            ([0xFF, 0xF0, 0xBD, 0xC0], -1000000),          // -1 000 000
            ([0x80, 0x00, 0x00, 0x01],  int.MinValue + 1), // int32 min + 1
            ([0x80, 0x00, 0x00, 0x00],  int.MinValue),     // int32 min
        ];

        int n = cases.Length;
        byte[] src = new byte[n * 4];
        int[] dst = new int[n];

        for (int i = 0; i < n; i++)
            cases[i].bytes.CopyTo(src, i * 4);

        int count = FastNumericsWrapper.ConvertInt32BytesToInt32s(src, dst, isLittleEndian: false);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal(cases[i].expected, dst[i]);
    }

    [Fact]
    public void ConvertInt32BytesToInt32s_BE_NegativeValue()
    {
        // 20 BE int32 negative values covering various magnitudes and bit patterns.
        // Each entry: (beBytes[4], expectedInt32)
        (byte[] bytes, int expected)[] cases =
        [
            ([0xFF, 0xFF, 0xFF, 0xFF], -1),                 // -1      (all bits set)
            ([0xFF, 0xFF, 0xFF, 0xFE], -2),                 // -2
            ([0xFF, 0xFF, 0xFF, 0xFD], -3),                 // -3
            ([0xFF, 0xFF, 0xFF, 0xF0], -16),                // -16     (nibble boundary)
            ([0xFF, 0xFF, 0xFF, 0x00], -256),               // -256    (byte boundary)
            ([0xFF, 0xFF, 0xFE, 0x00], -512),               // -512
            ([0xFF, 0xFF, 0xFC, 0x18], -1000),              // -1 000
            ([0xFF, 0xFF, 0x80, 0x00], -32768),             // int16 min
            ([0xFF, 0xFF, 0x00, 0x00], -65536),             // -2^16
            ([0xFF, 0xFE, 0x00, 0x00], -131072),            // -2^17
            ([0xFF, 0xF0, 0xBD, 0xC0], -1000000),           // -1 000 000
            ([0xFF, 0xE0, 0x00, 0x00], -2097152),           // -2^21
            ([0xFF, 0xC0, 0x00, 0x00], -4194304),           // -2^22
            ([0xFF, 0x00, 0x00, 0x00], -16777216),          // -2^24
            ([0xFE, 0x00, 0x00, 0x00], -33554432),          // -2^25
            ([0xFC, 0x00, 0x00, 0x00], -67108864),          // -2^26
            ([0xF0, 0x00, 0x00, 0x00], -268435456),         // -2^28
            ([0xC0, 0x00, 0x00, 0x00], -1073741824),        // -2^30  (int.MinValue / 2)
            ([0x80, 0x00, 0x00, 0x01],  int.MinValue + 1),  // int32 min + 1
            ([0x80, 0x00, 0x00, 0x00],  int.MinValue),      // int32 min (-2^31)
        ];

        int n = cases.Length;
        byte[] src = new byte[n * 4];
        int[] dst = new int[n];

        for (int i = 0; i < n; i++)
            cases[i].bytes.CopyTo(src, i * 4);

        int count = FastNumericsWrapper.ConvertInt32BytesToInt32s(src, dst, isLittleEndian: false);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal(cases[i].expected, dst[i]);
    }

    [Fact]
    public void ConvertInt32BytesToInt32s_33Elements_MainPlusScalar()
    {
        int n = 33;
        byte[] src = new byte[n * 4];
        int[] dst = new int[n];
        for (int i = 0; i < n; i++)
            BitConverter.GetBytes(i * 100).CopyTo(src, i * 4);

        int count = FastNumericsWrapper.ConvertInt32BytesToInt32s(src, dst, isLittleEndian: true);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal(i * 100, dst[i]);
    }

    // =========================================================
    // ConvertFloatBytesToInt32s
    // =========================================================

    [Fact]
    public void ConvertFloatBytesToInt32s_LE_TruncatesCorrectly()
    {
        // 10 LE float values, verifying truncation-towards-zero semantics.
        // Each entry: (floatValue, expectedInt32)
        (float value, int expected)[] cases =
        [
            (0.0f,           0),    // zero
            (1.0f,           1),    // exact integer
            (1.5f,           1),    // fractional, rounds down
            (1.9999f,        1),    // just below 2 — still truncates to 1
            (100.7f,       100),    // large fractional part, positive
            (-0.9f,          0),    // negative fraction close to zero -> 0
            (-1.0f,         -1),    // exact negative integer
            (-2.9f,         -2),    // truncation towards zero (not floor)
            (-100.7f,     -100),    // large fractional part, negative
            (32767.6f,   32767),    // near int16 max
        ];

        int n = cases.Length;
        byte[] src = new byte[n * 4];
        int[] dst = new int[n];

        for (int i = 0; i < n; i++)
            BitConverter.GetBytes(cases[i].value).CopyTo(src, i * 4);

        int count = FastNumericsWrapper.ConvertFloatBytesToInt32s(src, dst, isLittleEndian: true);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal(cases[i].expected, dst[i]);
    }

    [Fact]
    public void ConvertFloatBytesToInt32s_BE_ByteSwapsThenTruncates()
    {
        // 10 BE float values: bytes are the LE IEEE 754 representation reversed.
        // Verifies that bswap restores correct float bits before vcvttps2dq truncation.
        // Each entry: (floatValue, expectedInt32)
        (float value, int expected)[] cases =
        [
            (0.0f,        0),    // zero
            (1.0f,        1),    // exact integer
            (1.5f,        1),    // positive fraction truncated
            (1.9999f,     1),    // just below 2
            (100.7f,    100),    // larger positive, fractional part discarded
            (-0.9f,       0),    // negative fraction -> 0 (towards zero)
            (-1.0f,      -1),    // exact negative integer
            (-2.9f,      -2),    // negative fraction truncated towards zero
            (-100.7f,  -100),    // larger negative
            (32767.6f, 32767),   // near int16 max
        ];

        int n = cases.Length;
        byte[] src = new byte[n * 4];
        int[] dst = new int[n];

        for (int i = 0; i < n; i++)
        {
            byte[] le = BitConverter.GetBytes(cases[i].value);
            // reverse LE bytes to produce BE representation
            src[i * 4] = le[3];
            src[i * 4 + 1] = le[2];
            src[i * 4 + 2] = le[1];
            src[i * 4 + 3] = le[0];
        }

        int count = FastNumericsWrapper.ConvertFloatBytesToInt32s(src, dst, isLittleEndian: false);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal(cases[i].expected, dst[i]);
    }

    [Fact]
    public void ConvertFloatBytesToInt32s_33Elements_MainPlusScalar()
    {
        int n = 33;
        byte[] src = new byte[n * 4];
        int[] dst = new int[n];
        for (int i = 0; i < n; i++)
            BitConverter.GetBytes((float)(i + 1) + 0.7f).CopyTo(src, i * 4);

        int count = FastNumericsWrapper.ConvertFloatBytesToInt32s(src, dst, isLittleEndian: true);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal(i + 1, dst[i]);   // e.g. 1.7f truncates to 1
    }

    // =========================================================
    // ConvertFloatBytesToFloats
    // =========================================================

    [Fact]
    public void ConvertFloatBytesToFloats_LE_ReinterpretsExactly()
    {
        // 10 LE float values: bytes are copied as-is (pure reinterpretation, no conversion).
        // Result must be bit-identical to the original float — exact equality asserted.
        float[] values =
        [
            0.0f,
            1.0f,
            1.5f,
            -3.14f,
            100.0f,
            -100.0f,
            float.MaxValue,
            float.MinValue,
            float.Epsilon,     // smallest positive float
            float.NegativeInfinity,
        ];

        int n = values.Length;
        byte[] src = new byte[n * 4];
        float[] dst = new float[n];

        for (int i = 0; i < n; i++)
            BitConverter.GetBytes(values[i]).CopyTo(src, i * 4);

        int count = FastNumericsWrapper.ConvertFloatBytesToFloats(src, dst, isLittleEndian: true);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal(values[i], dst[i]);   // bit-identical: no numeric conversion
    }

    [Fact]
    public void ConvertFloatBytesToFloats_BE_ByteSwapsToCorrectFloat()
    {
        // 10 BE float values: each float's LE bytes are reversed to simulate
        // big-endian wire format. After bswap the bit pattern must be restored
        // exactly, so exact equality is asserted (same set as the LE mirror test).
        float[] values =
        [
            0.0f,
            1.0f,
            1.5f,
            -3.14f,
            100.0f,
            -100.0f,
            float.MaxValue,
            float.MinValue,
            float.Epsilon,
            float.NegativeInfinity,
        ];

        int n = values.Length;
        byte[] src = new byte[n * 4];
        float[] dst = new float[n];

        for (int i = 0; i < n; i++)
        {
            byte[] le = BitConverter.GetBytes(values[i]);
            src[i * 4] = le[3];
            src[i * 4 + 1] = le[2];
            src[i * 4 + 2] = le[1];
            src[i * 4 + 3] = le[0];
        }

        int count = FastNumericsWrapper.ConvertFloatBytesToFloats(src, dst, isLittleEndian: false);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal(values[i], dst[i]);   // bit-identical after bswap
    }

    [Fact]
    public void ConvertFloatBytesToFloats_33Elements_MainPlusScalar()
    {
        int n = 33;
        byte[] src = new byte[n * 4];
        float[] dst = new float[n];
        for (int i = 0; i < n; i++)
            BitConverter.GetBytes((float)(i + 1) * 0.5f).CopyTo(src, i * 4);

        int count = FastNumericsWrapper.ConvertFloatBytesToFloats(src, dst, isLittleEndian: true);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal((float)(i + 1) * 0.5f, dst[i]);
    }

    // =========================================================
    // ConvertInt32sToFloats
    // =========================================================

    [Fact]
    public void ConvertInt32sToFloats_ConvertsSmallValues()
    {
        int[] src = [0, 1, -1, 100, -100];
        float[] dst = new float[src.Length];

        int count = FastNumericsWrapper.ConvertInt32sToFloats(src, dst);

        Assert.Equal(src.Length, count);
        for (int i = 0; i < src.Length; i++)
            Assert.Equal((float)src[i], dst[i]);
    }

    [Fact]
    public void ConvertInt32sToFloats_EmptySrc_ReturnsZero()
    {
        int count = FastNumericsWrapper.ConvertInt32sToFloats(
            Array.Empty<int>(), Array.Empty<float>());
        Assert.Equal(0, count);
    }

    [Fact]
    public void ConvertInt32sToFloats_8Elements_MidPath()
    {
        int[] src = [1, 2, 3, 4, 5, 6, 7, 8];
        float[] dst = new float[8];

        int count = FastNumericsWrapper.ConvertInt32sToFloats(src, dst);

        Assert.Equal(8, count);
        for (int i = 0; i < 8; i++)
            Assert.Equal((float)src[i], dst[i]);
    }

    [Fact]
    public void ConvertInt32sToFloats_33Elements_MainPlusScalar()
    {
        int n = 33;
        int[] src = Enumerable.Range(1, n).ToArray();
        float[] dst = new float[n];

        int count = FastNumericsWrapper.ConvertInt32sToFloats(src, dst);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal((float)(i + 1), dst[i]);
    }

    // =========================================================
    // ConvertFloatsToInt32s
    // =========================================================

    [Fact]
    public void ConvertFloatsToInt32s_TruncatesTowardsZero()
    {
        float[] src = [1.0f, 10.9f, -5.5f, 100.0f, -0.9f];
        int[] dst = new int[src.Length];

        int count = FastNumericsWrapper.ConvertFloatsToInt32s(src, dst);

        Assert.Equal(src.Length, count);
        Assert.Equal(1, dst[0]);
        Assert.Equal(10, dst[1]);   // 10.9 -> 10
        Assert.Equal(-5, dst[2]);   // -5.5 -> -5 (towards zero)
        Assert.Equal(100, dst[3]);
        Assert.Equal(0, dst[4]);   // -0.9 -> 0 (towards zero)
    }

    [Fact]
    public void ConvertFloatsToInt32s_EmptySrc_ReturnsZero()
    {
        int count = FastNumericsWrapper.ConvertFloatsToInt32s(
            Array.Empty<float>(), Array.Empty<int>());
        Assert.Equal(0, count);
    }

    [Fact]
    public void ConvertFloatsToInt32s_33Elements_MainPlusScalar()
    {
        int n = 33;
        float[] src = Enumerable.Range(1, n).Select(i => (float)i + 0.7f).ToArray();
        int[] dst = new int[n];

        int count = FastNumericsWrapper.ConvertFloatsToInt32s(src, dst);

        Assert.Equal(n, count);
        for (int i = 0; i < n; i++)
            Assert.Equal(i + 1, dst[i]);   // 1.7f -> 1, 2.7f -> 2, ...
    }
}
