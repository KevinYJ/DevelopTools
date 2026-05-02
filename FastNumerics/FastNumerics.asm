; ============================================================
; FastNumerics.asm
; NASM x64 | Windows ABI | AVX2
; ============================================================
; High-throughput array type conversion with endian awareness.
;
; Exported functions:
;   ConvertInt32BytesToFloats  - packed int32 bytes -> float[]
;   ConvertInt32BytesToInt32s  - packed int32 bytes -> int32[]
;   ConvertFloatBytesToInt32s  - packed float bytes -> int32[]
;   ConvertFloatBytesToFloats  - packed float bytes -> float[]
;   ConvertInt32sToFloats      - int32[]            -> float[]
;   ConvertFloatsToInt32s      - float[]            -> int32[]
;
; Throughput optimisations applied to every function:
;   - AVX2 YMM registers (256-bit): 8 elements per SIMD op
;   - 4x loop unrolling: 32 elements per main-loop iteration
;   - prefetchnta: 512-byte software lookahead (bypass L2/L3)
;   - vpshufb byte-swap: in-register bswap32 for BE input data
;
; Loop tiers per function:
;   main  - 32 elements / iteration (4x unrolled SIMD)
;   mid8  -  8 elements / iteration (single SIMD)
;   tail  -  1 element  / iteration (scalar)
;
; CPU requirement: Intel Haswell (2013+) / AMD Ryzen (2017+)
;   Minimum instruction set: AVX2 (vpshufb ymm requires AVX2)
;
; Windows x64 calling convention:
;   arg1=RCX  arg2=RDX  arg3=R8d  arg4=R9d
;   volatile YMM used: ymm0-ymm4  (ymm5-ymm15 not touched)
;   return: EAX = element count processed
; ============================================================

default rel

; ============================================================
; Read-only data: 32-byte-aligned byte-swap shuffle mask.
; When used with vpshufb ymm, reverses the 4 bytes within
; each 32-bit lane: [B0 B1 B2 B3] -> [B3 B2 B1 B0].
; Loaded into ymm4 once per BE-path entry; reused every iter.
; ============================================================
section .rdata
align 32
bswap_mask_avx:
    db  3, 2, 1, 0,  7, 6, 5, 4, 11,10, 9, 8, 15,14,13,12   ; lane 0
    db  3, 2, 1, 0,  7, 6, 5, 4, 11,10, 9, 8, 15,14,13,12   ; lane 1

section .text

; ============================================================
; ConvertInt32BytesToFloats
;
; Each group of 4 src bytes is interpreted as a 32-bit integer
; (byte-order corrected if BE), then converted numerically to
; a 32-bit float via vcvtdq2ps.
;
;   RCX = byte*  src
;   RDX = float* dst
;   R8d = int    count         (number of elements)
;   R9d = int    isLittleEndian (1 = LE/native, 0 = BE/network)
;   returns EAX = count
; ============================================================
global ConvertInt32BytesToFloats
ConvertInt32BytesToFloats:
    push rbp
    mov  rbp, rsp
    sub  rsp, 32

    xor  eax, eax
    test r8d, r8d
    jz   .done

    test r9d, r9d
    jz   .be_entry

    ; ---- LE: 32-element main loop (4x unrolled) ----
.le_main:
    lea  r10d, [rax + 31]
    cmp  r10d, r8d
    jge  .le_mid8

    prefetchnta [rcx + rax*4 + 512]
    vmovdqu   ymm0, [rcx + rax*4]
    vmovdqu   ymm1, [rcx + rax*4 + 32]
    vmovdqu   ymm2, [rcx + rax*4 + 64]
    vmovdqu   ymm3, [rcx + rax*4 + 96]
    vcvtdq2ps ymm0, ymm0
    vcvtdq2ps ymm1, ymm1
    vcvtdq2ps ymm2, ymm2
    vcvtdq2ps ymm3, ymm3
    vmovdqu   [rdx + rax*4],      ymm0
    vmovdqu   [rdx + rax*4 + 32], ymm1
    vmovdqu   [rdx + rax*4 + 64], ymm2
    vmovdqu   [rdx + rax*4 + 96], ymm3
    add  eax, 32
    jmp  .le_main

    ; ---- LE: 8-element mid loop ----
.le_mid8:
    lea  r10d, [rax + 7]
    cmp  r10d, r8d
    jge  .le_tail

    vmovdqu   ymm0, [rcx + rax*4]
    vcvtdq2ps ymm0, ymm0
    vmovdqu   [rdx + rax*4], ymm0
    add  eax, 8
    jmp  .le_mid8

    ; ---- LE: scalar tail ----
.le_tail:
    cmp  eax, r8d
    jge  .done
    mov      r10d, [rcx + rax*4]
    cvtsi2ss xmm0, r10d
    movss    [rdx + rax*4], xmm0
    inc  eax
    jmp  .le_tail

    ; ---- BE: load bswap mask once, then 32-element main loop ----
.be_entry:
    vmovdqa ymm4, [rel bswap_mask_avx]

.be_main:
    lea  r10d, [rax + 31]
    cmp  r10d, r8d
    jge  .be_mid8

    prefetchnta [rcx + rax*4 + 512]
    vmovdqu   ymm0, [rcx + rax*4]
    vmovdqu   ymm1, [rcx + rax*4 + 32]
    vmovdqu   ymm2, [rcx + rax*4 + 64]
    vmovdqu   ymm3, [rcx + rax*4 + 96]
    vpshufb   ymm0, ymm0, ymm4
    vpshufb   ymm1, ymm1, ymm4
    vpshufb   ymm2, ymm2, ymm4
    vpshufb   ymm3, ymm3, ymm4
    vcvtdq2ps ymm0, ymm0
    vcvtdq2ps ymm1, ymm1
    vcvtdq2ps ymm2, ymm2
    vcvtdq2ps ymm3, ymm3
    vmovdqu   [rdx + rax*4],      ymm0
    vmovdqu   [rdx + rax*4 + 32], ymm1
    vmovdqu   [rdx + rax*4 + 64], ymm2
    vmovdqu   [rdx + rax*4 + 96], ymm3
    add  eax, 32
    jmp  .be_main

    ; ---- BE: 8-element mid loop ----
.be_mid8:
    lea  r10d, [rax + 7]
    cmp  r10d, r8d
    jge  .be_tail

    vmovdqu   ymm0, [rcx + rax*4]
    vpshufb   ymm0, ymm0, ymm4
    vcvtdq2ps ymm0, ymm0
    vmovdqu   [rdx + rax*4], ymm0
    add  eax, 8
    jmp  .be_mid8

    ; ---- BE: scalar tail ----
.be_tail:
    cmp  eax, r8d
    jge  .done
    mov      r10d, [rcx + rax*4]
    bswap    r10d
    cvtsi2ss xmm0, r10d
    movss    [rdx + rax*4], xmm0
    inc  eax
    jmp  .be_tail

.done:
    mov  eax, r8d
    vzeroupper
    add  rsp, 32
    pop  rbp
    ret


; ============================================================
; ConvertInt32BytesToInt32s
;
; Each group of 4 src bytes is reinterpreted as a 32-bit
; integer (byte-order corrected if BE) and written to dst.
; No numeric conversion; pure byte-order fixup + copy.
;
;   RCX = byte*  src
;   RDX = int*   dst
;   R8d = int    count
;   R9d = int    isLittleEndian (1 = LE, 0 = BE)
;   returns EAX = count
; ============================================================
global ConvertInt32BytesToInt32s
ConvertInt32BytesToInt32s:
    push rbp
    mov  rbp, rsp
    sub  rsp, 32

    xor  eax, eax
    test r8d, r8d
    jz   .done

    test r9d, r9d
    jz   .be_entry

    ; ---- LE: 32-element main loop (4x unrolled) ----
.le_main:
    lea  r10d, [rax + 31]
    cmp  r10d, r8d
    jge  .le_mid8

    prefetchnta [rcx + rax*4 + 512]
    vmovdqu ymm0, [rcx + rax*4]
    vmovdqu ymm1, [rcx + rax*4 + 32]
    vmovdqu ymm2, [rcx + rax*4 + 64]
    vmovdqu ymm3, [rcx + rax*4 + 96]
    vmovdqu [rdx + rax*4],      ymm0
    vmovdqu [rdx + rax*4 + 32], ymm1
    vmovdqu [rdx + rax*4 + 64], ymm2
    vmovdqu [rdx + rax*4 + 96], ymm3
    add  eax, 32
    jmp  .le_main

    ; ---- LE: 8-element mid loop ----
.le_mid8:
    lea  r10d, [rax + 7]
    cmp  r10d, r8d
    jge  .le_tail

    vmovdqu ymm0, [rcx + rax*4]
    vmovdqu [rdx + rax*4], ymm0
    add  eax, 8
    jmp  .le_mid8

    ; ---- LE: scalar tail ----
.le_tail:
    cmp  eax, r8d
    jge  .done
    mov  r10d, [rcx + rax*4]
    mov  [rdx + rax*4], r10d
    inc  eax
    jmp  .le_tail

    ; ---- BE: load bswap mask once, then 32-element main loop ----
.be_entry:
    vmovdqa ymm4, [rel bswap_mask_avx]

.be_main:
    lea  r10d, [rax + 31]
    cmp  r10d, r8d
    jge  .be_mid8

    prefetchnta [rcx + rax*4 + 512]
    vmovdqu ymm0, [rcx + rax*4]
    vmovdqu ymm1, [rcx + rax*4 + 32]
    vmovdqu ymm2, [rcx + rax*4 + 64]
    vmovdqu ymm3, [rcx + rax*4 + 96]
    vpshufb ymm0, ymm0, ymm4
    vpshufb ymm1, ymm1, ymm4
    vpshufb ymm2, ymm2, ymm4
    vpshufb ymm3, ymm3, ymm4
    vmovdqu [rdx + rax*4],      ymm0
    vmovdqu [rdx + rax*4 + 32], ymm1
    vmovdqu [rdx + rax*4 + 64], ymm2
    vmovdqu [rdx + rax*4 + 96], ymm3
    add  eax, 32
    jmp  .be_main

    ; ---- BE: 8-element mid loop ----
.be_mid8:
    lea  r10d, [rax + 7]
    cmp  r10d, r8d
    jge  .be_tail

    vmovdqu ymm0, [rcx + rax*4]
    vpshufb ymm0, ymm0, ymm4
    vmovdqu [rdx + rax*4], ymm0
    add  eax, 8
    jmp  .be_mid8

    ; ---- BE: scalar tail ----
.be_tail:
    cmp  eax, r8d
    jge  .done
    mov   r10d, [rcx + rax*4]
    bswap r10d
    mov   [rdx + rax*4], r10d
    inc  eax
    jmp  .be_tail

.done:
    mov  eax, r8d
    vzeroupper
    add  rsp, 32
    pop  rbp
    ret


; ============================================================
; ConvertFloatBytesToInt32s
;
; Each group of 4 src bytes is interpreted as an IEEE 754
; single-precision float (byte-order corrected if BE), then
; truncated towards zero to int32 via vcvttps2dq.
;
;   RCX = byte*  src
;   RDX = int*   dst
;   R8d = int    count
;   R9d = int    isLittleEndian (1 = LE, 0 = BE)
;   returns EAX = count
; ============================================================
global ConvertFloatBytesToInt32s
ConvertFloatBytesToInt32s:
    push rbp
    mov  rbp, rsp
    sub  rsp, 32

    xor  eax, eax
    test r8d, r8d
    jz   .done

    test r9d, r9d
    jz   .be_entry

    ; ---- LE: 32-element main loop (4x unrolled) ----
.le_main:
    lea  r10d, [rax + 31]
    cmp  r10d, r8d
    jge  .le_mid8

    prefetchnta [rcx + rax*4 + 512]
    vmovdqu    ymm0, [rcx + rax*4]
    vmovdqu    ymm1, [rcx + rax*4 + 32]
    vmovdqu    ymm2, [rcx + rax*4 + 64]
    vmovdqu    ymm3, [rcx + rax*4 + 96]
    vcvttps2dq ymm0, ymm0
    vcvttps2dq ymm1, ymm1
    vcvttps2dq ymm2, ymm2
    vcvttps2dq ymm3, ymm3
    vmovdqu    [rdx + rax*4],      ymm0
    vmovdqu    [rdx + rax*4 + 32], ymm1
    vmovdqu    [rdx + rax*4 + 64], ymm2
    vmovdqu    [rdx + rax*4 + 96], ymm3
    add  eax, 32
    jmp  .le_main

    ; ---- LE: 8-element mid loop ----
.le_mid8:
    lea  r10d, [rax + 7]
    cmp  r10d, r8d
    jge  .le_tail

    vmovdqu    ymm0, [rcx + rax*4]
    vcvttps2dq ymm0, ymm0
    vmovdqu    [rdx + rax*4], ymm0
    add  eax, 8
    jmp  .le_mid8

    ; ---- LE: scalar tail ----
.le_tail:
    cmp  eax, r8d
    jge  .done
    movss     xmm0, [rcx + rax*4]
    cvttss2si r10d, xmm0
    mov       [rdx + rax*4], r10d
    inc  eax
    jmp  .le_tail

    ; ---- BE: load bswap mask once, then 32-element main loop ----
.be_entry:
    vmovdqa ymm4, [rel bswap_mask_avx]

.be_main:
    lea  r10d, [rax + 31]
    cmp  r10d, r8d
    jge  .be_mid8

    prefetchnta [rcx + rax*4 + 512]
    vmovdqu    ymm0, [rcx + rax*4]
    vmovdqu    ymm1, [rcx + rax*4 + 32]
    vmovdqu    ymm2, [rcx + rax*4 + 64]
    vmovdqu    ymm3, [rcx + rax*4 + 96]
    vpshufb    ymm0, ymm0, ymm4
    vpshufb    ymm1, ymm1, ymm4
    vpshufb    ymm2, ymm2, ymm4
    vpshufb    ymm3, ymm3, ymm4
    vcvttps2dq ymm0, ymm0
    vcvttps2dq ymm1, ymm1
    vcvttps2dq ymm2, ymm2
    vcvttps2dq ymm3, ymm3
    vmovdqu    [rdx + rax*4],      ymm0
    vmovdqu    [rdx + rax*4 + 32], ymm1
    vmovdqu    [rdx + rax*4 + 64], ymm2
    vmovdqu    [rdx + rax*4 + 96], ymm3
    add  eax, 32
    jmp  .be_main

    ; ---- BE: 8-element mid loop ----
.be_mid8:
    lea  r10d, [rax + 7]
    cmp  r10d, r8d
    jge  .be_tail

    vmovdqu    ymm0, [rcx + rax*4]
    vpshufb    ymm0, ymm0, ymm4
    vcvttps2dq ymm0, ymm0
    vmovdqu    [rdx + rax*4], ymm0
    add  eax, 8
    jmp  .be_mid8

    ; ---- BE: scalar tail ----
.be_tail:
    cmp  eax, r8d
    jge  .done
    mov      r10d, [rcx + rax*4]
    bswap    r10d
    movd     xmm0, r10d
    cvttss2si r10d, xmm0
    mov      [rdx + rax*4], r10d
    inc  eax
    jmp  .be_tail

.done:
    mov  eax, r8d
    vzeroupper
    add  rsp, 32
    pop  rbp
    ret


; ============================================================
; ConvertFloatBytesToFloats
;
; Each group of 4 src bytes is reinterpreted as an IEEE 754
; float (byte-order corrected if BE) and written to dst.
; No numeric conversion; pure byte-order fixup + copy.
;
;   RCX = byte*  src
;   RDX = float* dst
;   R8d = int    count
;   R9d = int    isLittleEndian (1 = LE, 0 = BE)
;   returns EAX = count
; ============================================================
global ConvertFloatBytesToFloats
ConvertFloatBytesToFloats:
    push rbp
    mov  rbp, rsp
    sub  rsp, 32

    xor  eax, eax
    test r8d, r8d
    jz   .done

    test r9d, r9d
    jz   .be_entry

    ; ---- LE: 32-element main loop (4x unrolled) ----
.le_main:
    lea  r10d, [rax + 31]
    cmp  r10d, r8d
    jge  .le_mid8

    prefetchnta [rcx + rax*4 + 512]
    vmovdqu ymm0, [rcx + rax*4]
    vmovdqu ymm1, [rcx + rax*4 + 32]
    vmovdqu ymm2, [rcx + rax*4 + 64]
    vmovdqu ymm3, [rcx + rax*4 + 96]
    vmovdqu [rdx + rax*4],      ymm0
    vmovdqu [rdx + rax*4 + 32], ymm1
    vmovdqu [rdx + rax*4 + 64], ymm2
    vmovdqu [rdx + rax*4 + 96], ymm3
    add  eax, 32
    jmp  .le_main

    ; ---- LE: 8-element mid loop ----
.le_mid8:
    lea  r10d, [rax + 7]
    cmp  r10d, r8d
    jge  .le_tail

    vmovdqu ymm0, [rcx + rax*4]
    vmovdqu [rdx + rax*4], ymm0
    add  eax, 8
    jmp  .le_mid8

    ; ---- LE: scalar tail ----
.le_tail:
    cmp  eax, r8d
    jge  .done
    mov  r10d, [rcx + rax*4]
    mov  [rdx + rax*4], r10d
    inc  eax
    jmp  .le_tail

    ; ---- BE: load bswap mask once, then 32-element main loop ----
.be_entry:
    vmovdqa ymm4, [rel bswap_mask_avx]

.be_main:
    lea  r10d, [rax + 31]
    cmp  r10d, r8d
    jge  .be_mid8

    prefetchnta [rcx + rax*4 + 512]
    vmovdqu ymm0, [rcx + rax*4]
    vmovdqu ymm1, [rcx + rax*4 + 32]
    vmovdqu ymm2, [rcx + rax*4 + 64]
    vmovdqu ymm3, [rcx + rax*4 + 96]
    vpshufb ymm0, ymm0, ymm4
    vpshufb ymm1, ymm1, ymm4
    vpshufb ymm2, ymm2, ymm4
    vpshufb ymm3, ymm3, ymm4
    vmovdqu [rdx + rax*4],      ymm0
    vmovdqu [rdx + rax*4 + 32], ymm1
    vmovdqu [rdx + rax*4 + 64], ymm2
    vmovdqu [rdx + rax*4 + 96], ymm3
    add  eax, 32
    jmp  .be_main

    ; ---- BE: 8-element mid loop ----
.be_mid8:
    lea  r10d, [rax + 7]
    cmp  r10d, r8d
    jge  .be_tail

    vmovdqu ymm0, [rcx + rax*4]
    vpshufb ymm0, ymm0, ymm4
    vmovdqu [rdx + rax*4], ymm0
    add  eax, 8
    jmp  .be_mid8

    ; ---- BE: scalar tail ----
.be_tail:
    cmp  eax, r8d
    jge  .done
    mov   r10d, [rcx + rax*4]
    bswap r10d
    mov   [rdx + rax*4], r10d
    inc  eax
    jmp  .be_tail

.done:
    mov  eax, r8d
    vzeroupper
    add  rsp, 32
    pop  rbp
    ret


; ============================================================
; ConvertInt32sToFloats
;
; Converts each int32 element to its float equivalent via
; vcvtdq2ps (IEEE 754 round-to-nearest).
; Assumes native (little-endian) byte order; no byte-swap.
;
;   RCX = int*   src
;   RDX = float* dst
;   R8d = int    count
;   returns EAX = count
; ============================================================
global ConvertInt32sToFloats
ConvertInt32sToFloats:
    push rbp
    mov  rbp, rsp
    sub  rsp, 32

    xor  eax, eax
    test r8d, r8d
    jz   .done

    ; ---- 32-element main loop (4x unrolled) ----
.main:
    lea  r10d, [rax + 31]
    cmp  r10d, r8d
    jge  .mid8

    prefetchnta [rcx + rax*4 + 512]
    vmovdqu   ymm0, [rcx + rax*4]
    vmovdqu   ymm1, [rcx + rax*4 + 32]
    vmovdqu   ymm2, [rcx + rax*4 + 64]
    vmovdqu   ymm3, [rcx + rax*4 + 96]
    vcvtdq2ps ymm0, ymm0
    vcvtdq2ps ymm1, ymm1
    vcvtdq2ps ymm2, ymm2
    vcvtdq2ps ymm3, ymm3
    vmovdqu   [rdx + rax*4],      ymm0
    vmovdqu   [rdx + rax*4 + 32], ymm1
    vmovdqu   [rdx + rax*4 + 64], ymm2
    vmovdqu   [rdx + rax*4 + 96], ymm3
    add  eax, 32
    jmp  .main

    ; ---- 8-element mid loop ----
.mid8:
    lea  r10d, [rax + 7]
    cmp  r10d, r8d
    jge  .tail

    vmovdqu   ymm0, [rcx + rax*4]
    vcvtdq2ps ymm0, ymm0
    vmovdqu   [rdx + rax*4], ymm0
    add  eax, 8
    jmp  .mid8

    ; ---- scalar tail ----
.tail:
    cmp  eax, r8d
    jge  .done
    mov      r10d, [rcx + rax*4]
    cvtsi2ss xmm0, r10d
    movss    [rdx + rax*4], xmm0
    inc  eax
    jmp  .tail

.done:
    mov  eax, r8d
    vzeroupper
    add  rsp, 32
    pop  rbp
    ret


; ============================================================
; ConvertFloatsToInt32s
;
; Truncates each float element towards zero to int32 via
; vcvttps2dq (equivalent to C cast (int)f).
; Assumes native (little-endian) byte order; no byte-swap.
;
;   RCX = float* src
;   RDX = int*   dst
;   R8d = int    count
;   returns EAX = count
; ============================================================
global ConvertFloatsToInt32s
ConvertFloatsToInt32s:
    push rbp
    mov  rbp, rsp
    sub  rsp, 32

    xor  eax, eax
    test r8d, r8d
    jz   .done

    ; ---- 32-element main loop (4x unrolled) ----
.main:
    lea  r10d, [rax + 31]
    cmp  r10d, r8d
    jge  .mid8

    prefetchnta [rcx + rax*4 + 512]
    vmovdqu    ymm0, [rcx + rax*4]
    vmovdqu    ymm1, [rcx + rax*4 + 32]
    vmovdqu    ymm2, [rcx + rax*4 + 64]
    vmovdqu    ymm3, [rcx + rax*4 + 96]
    vcvttps2dq ymm0, ymm0
    vcvttps2dq ymm1, ymm1
    vcvttps2dq ymm2, ymm2
    vcvttps2dq ymm3, ymm3
    vmovdqu    [rdx + rax*4],      ymm0
    vmovdqu    [rdx + rax*4 + 32], ymm1
    vmovdqu    [rdx + rax*4 + 64], ymm2
    vmovdqu    [rdx + rax*4 + 96], ymm3
    add  eax, 32
    jmp  .main

    ; ---- 8-element mid loop ----
.mid8:
    lea  r10d, [rax + 7]
    cmp  r10d, r8d
    jge  .tail

    vmovdqu    ymm0, [rcx + rax*4]
    vcvttps2dq ymm0, ymm0
    vmovdqu    [rdx + rax*4], ymm0
    add  eax, 8
    jmp  .mid8

    ; ---- scalar tail ----
.tail:
    cmp  eax, r8d
    jge  .done
    movss     xmm0, [rcx + rax*4]
    cvttss2si r10d, xmm0
    mov       [rdx + rax*4], r10d
    inc  eax
    jmp  .tail

.done:
    mov  eax, r8d
    vzeroupper
    add  rsp, 32
    pop  rbp
    ret
