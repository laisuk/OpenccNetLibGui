/*
 * CjkEncodingDetector.cs
 *
 * Pure C# CJK/Unicode encoding detector.
 *
 * Unicode/BOM/UTF-16 detection: project code.
 * Big5/GB18030 statistical fallback: adapted from uchardet 0.0.5 /
 * Mozilla Universal Charset Detector.
 *
 * ***** BEGIN LICENSE BLOCK *****
 * Version: MPL 1.1/GPL 2.0/LGPL 2.1
 *
 * The contents of this file derived from Mozilla charset detector code are
 * subject to the Mozilla Public License Version 1.1 (the "License"); you may
 * not use those portions except in compliance with the License. You may obtain
 * a copy of the License at http://www.mozilla.org/MPL/
 *
 * Software distributed under the License is distributed on an "AS IS" basis,
 * WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License
 * for the specific language governing rights and limitations under the License.
 *
 * The Original Code is Mozilla charset detector code.
 *
 * The Initial Developer of the Original Code is
 * Netscape Communications Corporation.
 * Portions created by the Initial Developer are Copyright (C) 1998-2001
 * the Initial Developer. All Rights Reserved.
 *
 * Alternatively, the contents derived from Mozilla code may be used under the
 * terms of either the GNU General Public License Version 2 or later (the
 * "GPL"), or the GNU Lesser General Public License Version 2.1 or later
 * (the "LGPL"), in which case the provisions of the GPL or LGPL are applicable
 * instead of those above.
 *
 * ***** END LICENSE BLOCK *****
 */

using System;

namespace OpenccNetLibGui.Helpers
{
    /// <summary>
    /// Detects Unicode encodings deterministically and falls back to a small,
    /// Chinese-only statistical detector for Big5 and GB18030-family text.
    /// </summary>
    /// <remarks>
    /// Detection order:
    /// BOM -> ASCII -> strict UTF-8 -> BOM-less UTF-16 heuristic ->
    /// Big5/GB18030 statistical fallback -> Unknown.
    ///
    /// GB2312 and GBK are reported as GB18030 because GB18030 is the decoder
    /// superset used by the caller.
    /// </remarks>
    public static class CjkEncodingDetector
    {
        private const int MaxHeuristicSampleSize = 128 * 1024;
        private const float MinLegacyConfidence = 0.20F;
        private const float MinLegacyMargin = 0.05F;

        private const int StateStart = 0;
        private const int StateError = 1;
        private const int StateItsMe = 2;

        public enum EncodingKind
        {
            Unknown,
            Ascii,
            Utf8,
            Utf8Bom,
            Utf16Le,
            Utf16LeBom,
            Utf16Be,
            Utf16BeBom,
            Big5,
            Gb18030
        }

        public readonly struct Result
        {
            public Result(EncodingKind encoding, int bomSize, float confidence)
            {
                Encoding = encoding;
                BomSize = bomSize;
                Confidence = confidence;
            }

            public EncodingKind Encoding { get; }
            public int BomSize { get; }
            public float Confidence { get; }

            public bool Detected => Encoding != EncodingKind.Unknown;
            public bool HasBom => BomSize != 0;

            public bool IsUnicode
            {
                get
                {
                    return Encoding switch
                    {
                        EncodingKind.Ascii or EncodingKind.Utf8 or EncodingKind.Utf8Bom or EncodingKind.Utf16Le
                            or EncodingKind.Utf16LeBom or EncodingKind.Utf16Be or EncodingKind.Utf16BeBom => true,
                        _ => false
                    };
                }
            }

            public override string ToString()
            {
                return GetEncodingName(Encoding);
            }
        }

        /// <summary>
        /// Detects an encoding from the supplied byte array.
        /// </summary>
        public static Result Detect(byte[] data)
        {
            return data == null ? throw new ArgumentNullException(nameof(data)) : Detect(data, 0, data.Length);
        }

        /// <summary>
        /// Detects an encoding from a byte-array range without allocating a slice.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Result Detect(byte[] data, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (offset < 0 || count < 0 || offset > data.Length - count)
                throw new ArgumentOutOfRangeException();

            if (count == 0)
                return Unknown();

            // 1. BOM detection.
            if (StartsWith(data, offset, count, 0xEF, 0xBB, 0xBF))
                return new Result(EncodingKind.Utf8Bom, 3, 1.0F);

            if (StartsWith(data, offset, count, 0xFF, 0xFE))
                return new Result(EncodingKind.Utf16LeBom, 2, 1.0F);

            if (StartsWith(data, offset, count, 0xFE, 0xFF))
                return new Result(EncodingKind.Utf16BeBom, 2, 1.0F);

            // 2-3. Classify ASCII / strict UTF-8 in one full-range pass.
            // UTF-8 remains an exact result: the entire requested range is validated.
            var utf8Kind = ClassifyUtf8(data, offset, count);
            if (utf8Kind != EncodingKind.Unknown)
                return new Result(utf8Kind, 0, 1.0F);

            // 4. UTF-16 without BOM.
            if (LooksLikeUtf16Le(data, offset, count))
                return new Result(EncodingKind.Utf16Le, 0, 0.90F);

            if (LooksLikeUtf16Be(data, offset, count))
                return new Result(EncodingKind.Utf16Be, 0, 0.90F);

            // 5. Chinese legacy fallback.
            return DetectChineseLegacy(data, offset, count);
        }

        public static string GetEncodingName(EncodingKind encoding)
        {
            return encoding switch
            {
                EncodingKind.Ascii => "ASCII",
                EncodingKind.Utf8 => "UTF-8",
                EncodingKind.Utf8Bom => "UTF-8 BOM",
                EncodingKind.Utf16Le => "UTF-16 LE",
                EncodingKind.Utf16LeBom => "UTF-16 LE BOM",
                EncodingKind.Utf16Be => "UTF-16 BE",
                EncodingKind.Utf16BeBom => "UTF-16 BE BOM",
                EncodingKind.Big5 => "Big5",
                EncodingKind.Gb18030 => "GB18030",
                _ => "Unknown"
            };
        }

        private static Result Unknown()
        {
            return new Result(EncodingKind.Unknown, 0, 0.0F);
        }

        private static bool StartsWith(
            byte[] data, int offset, int count,
            byte b0, byte b1)
        {
            return count >= 2 &&
                   data[offset] == b0 &&
                   data[offset + 1] == b1;
        }

        private static bool StartsWith(
            byte[] data, int offset, int count,
            byte b0, byte b1, byte b2)
        {
            return count >= 3 &&
                   data[offset] == b0 &&
                   data[offset + 1] == b1 &&
                   data[offset + 2] == b2;
        }

        private static EncodingKind ClassifyUtf8(byte[] data, int offset, int count)
        {
            var i = offset;
            var end = offset + count;
            var ascii = true;

            while (i < end)
            {
                var c0 = data[i];

                if (c0 <= 0x7F)
                {
                    i++;
                    continue;
                }

                ascii = false;

                switch (c0)
                {
                    case >= 0xC2 and <= 0xDF when i + 1 >= end || (data[i + 1] & 0xC0) != 0x80:
                        return EncodingKind.Unknown;
                    case >= 0xC2 and <= 0xDF:
                        i += 2;
                        continue;
                    case >= 0xE0 and <= 0xEF when i + 2 >= end:
                        return EncodingKind.Unknown;
                    case >= 0xE0 and <= 0xEF:
                    {
                        var c1 = data[i + 1];
                        var c2 = data[i + 2];
                        if ((c2 & 0xC0) != 0x80)
                            return EncodingKind.Unknown;

                        switch (c0)
                        {
                            case 0xE0:
                            {
                                if (c1 is < 0xA0 or > 0xBF)
                                    return EncodingKind.Unknown;
                                break;
                            }
                            case 0xED:
                            {
                                // Reject UTF-16 surrogate range U+D800 to U+DFFF.
                                if (c1 is < 0x80 or > 0x9F)
                                    return EncodingKind.Unknown;
                                break;
                            }
                            default:
                            {
                                if ((c1 & 0xC0) != 0x80)
                                {
                                    return EncodingKind.Unknown;
                                }

                                break;
                            }
                        }

                        i += 3;
                        continue;
                    }
                    case >= 0xF0 and <= 0xF4 when i + 3 >= end:
                        return EncodingKind.Unknown;
                    case >= 0xF0 and <= 0xF4:
                    {
                        var c1 = data[i + 1];
                        var c2 = data[i + 2];
                        var c3 = data[i + 3];
                        if ((c2 & 0xC0) != 0x80 || (c3 & 0xC0) != 0x80)
                            return EncodingKind.Unknown;

                        switch (c0)
                        {
                            case 0xF0:
                            {
                                if (c1 is < 0x90 or > 0xBF)
                                    return EncodingKind.Unknown;
                                break;
                            }
                            case 0xF4:
                            {
                                if (c1 is < 0x80 or > 0x8F)
                                    return EncodingKind.Unknown;
                                break;
                            }
                            default:
                            {
                                if ((c1 & 0xC0) != 0x80)
                                {
                                    return EncodingKind.Unknown;
                                }

                                break;
                            }
                        }

                        i += 4;
                        continue;
                    }
                    default:
                        return EncodingKind.Unknown;
                }
            }

            return ascii ? EncodingKind.Ascii : EncodingKind.Utf8;
        }

        private static bool IsValidUtf8(byte[] data, int offset, int count)
        {
            var kind = ClassifyUtf8(data, offset, count);
            return kind is EncodingKind.Ascii or EncodingKind.Utf8;
        }

        private static bool LooksLikeUtf16Le(byte[] data, int offset, int count)
        {
            return LooksLikeUtf16(data, offset, count, 1);
        }

        private static bool LooksLikeUtf16Be(byte[] data, int offset, int count)
        {
            return LooksLikeUtf16(data, offset, count, 0);
        }

        private static bool LooksLikeUtf16(byte[] data, int offset, int count, int zeroByteIndex)
        {
            // Preserve the whole-range structural requirement, but bound the
            // statistical zero-byte heuristic so huge files are not rescanned.
            if (count < 4 || (count & 1) != 0)
                return false;

            var sampleCount = Math.Min(count, MaxHeuristicSampleSize) & ~1;
            var end = offset + sampleCount;
            var zeroBytes = 0;
            var pairs = 0;

            for (var i = offset; i < end; i += 2)
            {
                var first = data[i];
                var second = data[i + 1];
                var zeroByte = zeroByteIndex == 0 ? first : second;
                var otherByte = zeroByteIndex == 0 ? second : first;

                if (zeroByte == 0 && otherByte != 0)
                    zeroBytes++;

                pairs++;
            }

            return pairs != 0 && zeroBytes * 100 / pairs >= 60;
        }

        private static Result DetectChineseLegacy(byte[] data, int offset, int count)
        {
            var sampleCount = Math.Min(count, MaxHeuristicSampleSize);

            var big5Confidence = ProbeBig5(data, offset, sampleCount);
            var gb18030Confidence = ProbeGb18030(data, offset, sampleCount);

            var best = Math.Max(big5Confidence, gb18030Confidence);
            var second = Math.Min(big5Confidence, gb18030Confidence);

            if (best < MinLegacyConfidence ||
                best - second < MinLegacyMargin)
            {
                return Unknown();
            }

            return big5Confidence > gb18030Confidence
                ? new Result(EncodingKind.Big5, 0, best)
                : new Result(EncodingKind.Gb18030, 0, best);
        }

        private static float ProbeBig5(byte[] data, int offset, int count)
        {
            var stateMachine = new CodingStateMachine(
                Big5ClassTable,
                5,
                Big5StateTable,
                Big5CharLenTable);

            var distribution = new DistributionAnalysis(
                Big5FrequentBits,
                5376,
                0.75F,
                GetBig5Order);

            FeedProber(data, offset, count, stateMachine, distribution);
            return distribution.GetConfidence();
        }

        private static float ProbeGb18030(byte[] data, int offset, int count)
        {
            var stateMachine = new CodingStateMachine(
                Gb18030ClassTable,
                7,
                Gb18030StateTable,
                Gb18030CharLenTable);

            var distribution = new DistributionAnalysis(
                Gb2312FrequentBits,
                3760,
                0.90F,
                GetGb2312Order);

            FeedProber(data, offset, count, stateMachine, distribution);
            return distribution.GetConfidence();
        }

        private static void FeedProber(
            byte[] data,
            int offset,
            int count,
            CodingStateMachine stateMachine,
            DistributionAnalysis distribution)
        {
            var end = offset + count;

            for (var i = offset; i < end; i++)
            {
                var state = stateMachine.NextState(data[i]);

                if (state == StateItsMe)
                    break;

                if (state != StateStart) continue;
                var charLen = stateMachine.CurrentCharLen;

                // The detector is fed as one contiguous sample, therefore
                // every completed 2-byte character has its lead byte at i-1.
                if (charLen == 2 && i > offset)
                    distribution.HandleOneChar(data[i - 1], data[i]);
            }
        }

        private delegate int OrderGetter(byte first, byte second);

        private sealed class DistributionAnalysis
        {
            private const int MinimumDataThreshold = 4;
            private readonly uint[] _frequentBits;
            private readonly int _tableSize;
            private readonly float _typicalDistributionRatio;
            private readonly OrderGetter _getOrder;

            private int _totalChars;
            private int _freqChars;

            public DistributionAnalysis(
                uint[] frequentBits,
                int tableSize,
                float typicalDistributionRatio,
                OrderGetter getOrder)
            {
                _frequentBits = frequentBits;
                _tableSize = tableSize;
                _typicalDistributionRatio = typicalDistributionRatio;
                _getOrder = getOrder;
            }

            public void HandleOneChar(byte first, byte second)
            {
                var order = _getOrder(first, second);
                if (order < 0)
                    return;

                _totalChars++;

                if (order < _tableSize && IsFrequent(_frequentBits, order))
                    _freqChars++;
            }

            public float GetConfidence()
            {
                if (_totalChars <= 0 || _freqChars <= MinimumDataThreshold)
                    return 0.01F;

                if (_totalChars == _freqChars) return 0.99F;
                var ratio =
                    _freqChars /
                    ((_totalChars - _freqChars) * _typicalDistributionRatio);

                return ratio < 0.99F ? ratio : 0.99F;
            }
        }

        private sealed class CodingStateMachine
        {
            private readonly byte[] _classTable;
            private readonly int _classFactor;
            private readonly byte[] _stateTable;
            private readonly byte[] _charLenTable;

            private int _currentState = StateStart;

            public CodingStateMachine(
                byte[] classTable,
                int classFactor,
                byte[] stateTable,
                byte[] charLenTable)
            {
                _classTable = classTable;
                _classFactor = classFactor;
                _stateTable = stateTable;
                _charLenTable = charLenTable;
            }

            public int CurrentCharLen { get; private set; }

            public int NextState(byte value)
            {
                int byteClass = _classTable[value];

                if (_currentState == StateStart)
                    CurrentCharLen = _charLenTable[byteClass];

                _currentState =
                    _stateTable[_currentState * _classFactor + byteClass];

                return _currentState;
            }
        }

        private static int GetGb2312Order(byte first, byte second)
        {
            if (first >= 0xB0 && second >= 0xA1)
                return 94 * (first - 0xB0) + second - 0xA1;

            return -1;
        }

        private static int GetBig5Order(byte first, byte second)
        {
            if (first < 0xA4)
                return -1;

            if (second >= 0xA1)
                return 157 * (first - 0xA4) + second - 0xA1 + 63;

            return 157 * (first - 0xA4) + second - 0x40;
        }

        private static bool IsFrequent(uint[] bits, int order)
        {
            return (bits[order >> 5] & (1u << (order & 31))) != 0;
        }

        // These state-machine tables are the unpacked Big5/GB18030 models
        // from uchardet 0.0.5 nsMBCSSM.cpp.

        private static readonly byte[] Big5ClassTable =
        {
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1,
            4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
            4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
            4, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 0,
        };

        private static readonly byte[] Big5StateTable =
        {
            1, 0, 0, 3, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 1,
            1, 0, 0, 0, 0, 0, 0, 0,
        };

        private static readonly byte[] Big5CharLenTable =
        {
            0, 1, 1, 2, 0
        };

        private static readonly byte[] Gb18030ClassTable =
        {
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 4,
            5, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
            6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
            6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
            6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
            6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
            6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
            6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
            6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 0,
        };

        private static readonly byte[] Gb18030StateTable =
        {
            1, 0, 0, 0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 2, 2,
            2, 2, 2, 2, 2, 1, 1, 0, 4, 1, 0, 0, 1, 1, 1, 1,
            1, 1, 5, 1, 1, 1, 2, 1, 1, 1, 0, 0, 0, 0, 0, 0,
        };

        private static readonly byte[] Gb18030CharLenTable =
        {
            0, 1, 1, 1, 1, 1, 2
        };

        // uchardet's distribution algorithm only asks whether a frequency
        // rank is in the top 512. Keeping one bit per table entry preserves
        // that behavior while avoiding ~9K Int16 values in a single-file port.

        private static readonly uint[] Big5FrequentBits =
        {
            0x20BA8DE9u, 0x40E91D50u, 0x4B4C4826u, 0x3012B810u, 0xA08482CDu, 0x17200A22u, 0x2A264062u, 0x14C0A142u,
            0x04891300u, 0x0F2CC002u, 0x198F8400u, 0x03022704u, 0x00070831u, 0x44180E04u, 0x05A00831u, 0x00011142u,
            0x00000001u, 0x00044000u, 0x00888808u, 0x01C00511u, 0x80000082u, 0xA0A10180u, 0x07201001u, 0x4074002Au,
            0x40005021u, 0x20400008u, 0x00000100u, 0xA2000102u, 0x00000002u, 0x04308225u, 0x02000000u, 0x00800010u,
            0x21021640u, 0x22002000u, 0x04488038u, 0x06090000u, 0x00200200u, 0x00022000u, 0x00000423u, 0x01804024u,
            0x02001000u, 0x00400082u, 0x00008000u, 0x8004020Au, 0x00000004u, 0x20280008u, 0x6D104000u, 0x00C08000u,
            0x40008000u, 0x00000000u, 0x01000888u, 0x00000000u, 0x00A20250u, 0x11010040u, 0x00040000u, 0x00100020u,
            0x00200100u, 0x20000000u, 0x00000020u, 0x08010800u, 0x21010000u, 0x00008014u, 0x00802A14u, 0x00001010u,
            0x060000C0u, 0x0108050Cu, 0x20801000u, 0x20000080u, 0x00008000u, 0x00400008u, 0x00839000u, 0x01010180u,
            0x00000204u, 0x00200000u, 0x00018412u, 0x001410D4u, 0x20800003u, 0x00810002u, 0x40000240u, 0x00000100u,
            0x00020000u, 0x0000A008u, 0x00000000u, 0x06000000u, 0x10040000u, 0x20010100u, 0x00000010u, 0x00000048u,
            0x12040008u, 0x08080000u, 0xA1000280u, 0x00008000u, 0x00000010u, 0x03A00000u, 0x04000000u, 0x00000028u,
            0x01001000u, 0x00040000u, 0x02000000u, 0x00200810u, 0x08000020u, 0x40A86000u, 0x20401000u, 0x000020B8u,
            0x01040000u, 0x00040000u, 0x00026000u, 0x00004200u, 0x00000000u, 0x20040000u, 0x00000000u, 0x04100000u,
            0x00010080u, 0x00002C00u, 0x04404000u, 0x00012000u, 0x00480000u, 0x00040800u, 0x00008000u, 0x0000D800u,
            0x10000000u, 0x00001042u, 0x00001000u, 0x00000000u, 0x00000400u, 0x00000000u, 0x08000508u, 0x00000000u,
            0x00000000u, 0x00000000u, 0x02800200u, 0x00080410u, 0x00000080u, 0x00000000u, 0x00000000u, 0x01000000u,
            0x12000000u, 0x00000080u, 0x00000020u, 0x20000000u, 0x00000000u, 0x50020008u, 0x00000000u, 0x02800200u,
            0x00000020u, 0x00010000u, 0x00000001u, 0x00008000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000210u,
            0x00000080u, 0x00000000u, 0x00000000u, 0x00080000u, 0x82800020u, 0x00000000u, 0x00000000u, 0x000A8000u,
            0x00010000u, 0x00010000u, 0x00000000u, 0x00180000u, 0x00000001u, 0x40800000u, 0x01000010u, 0x00220000u,
        };

        private static readonly uint[] Gb2312FrequentBits =
        {
            0x008A0000u, 0x01010000u, 0x08000800u, 0x09204021u, 0x00200020u, 0x20002482u, 0x05C00000u, 0x04000213u,
            0x34200010u, 0x00004001u, 0x00008025u, 0x20008000u, 0x00000804u, 0x10000424u, 0x04028640u, 0x23A60141u,
            0x1100A000u, 0x48001000u, 0x08008004u, 0x000800C0u, 0x020A0800u, 0x01414020u, 0x01004084u, 0x20008000u,
            0x810C0601u, 0x40204400u, 0x814A09E1u, 0x00400040u, 0x00098320u, 0x00004580u, 0x47004000u, 0x40000080u,
            0x038B2000u, 0x00000005u, 0x4C800402u, 0xA0C4004Cu, 0x0612640Au, 0x00000904u, 0x00052024u, 0x81120001u,
            0x286010C0u, 0x00415002u, 0x12010004u, 0x92000005u, 0x00200800u, 0x08005480u, 0x0080A000u, 0x00081000u,
            0x80005000u, 0x86007000u, 0x14000288u, 0x00083100u, 0x00100200u, 0x00040000u, 0x00400030u, 0x00000100u,
            0x43103000u, 0x80010001u, 0x04110400u, 0x40400000u, 0x000A0140u, 0x40000002u, 0x00000008u, 0x00000000u,
            0x00008000u, 0x00010400u, 0x00802000u, 0x00000848u, 0x00010002u, 0x04420000u, 0x04910210u, 0x64800640u,
            0x04400010u, 0x01001800u, 0x12000400u, 0x4C300040u, 0x56121080u, 0x48062F97u, 0x001000C7u, 0x42800101u,
            0x000020A4u, 0x00000004u, 0x80008074u, 0x80000000u, 0x10081300u, 0x19022000u, 0x00000C40u, 0x04808080u,
            0x40042001u, 0x00202180u, 0x04140102u, 0x00410008u, 0x82800208u, 0x18189103u, 0x00001145u, 0x0008B41Au,
            0x40801080u, 0x00080010u, 0x00003010u, 0x00600140u, 0x01412020u, 0x20259000u, 0x81082013u, 0x50000208u,
            0x00088284u, 0x00400010u, 0x021A0912u, 0x00030004u, 0x00002288u, 0x04064000u, 0x08900000u, 0x248D4080u,
            0x2212491Eu, 0x0001AA0Au, 0x0A440400u, 0x08402002u, 0x84412030u, 0x00000180u,
        };
    }
}