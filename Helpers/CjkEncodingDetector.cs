/*
 * CjkEncodingDetector.cs
 *
 * Pure C# CJK/Unicode encoding detector.
 *
 * Unicode/BOM/UTF-16 detection: project code.
 * Big5/GB18030/Shift-JIS statistical fallback: adapted from uchardet 0.0.5 /
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
    /// compact CJK statistical detector for Big5, GB18030-family, and Shift-JIS text.
    /// </summary>
    /// <remarks>
    /// Detection order:
    /// BOM -> ASCII -> strict UTF-8 -> BOM-less UTF-16 heuristic ->
    /// Big5/GB18030/Shift-JIS statistical fallback -> Unknown.
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
            Gb18030,
            ShiftJis
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

            // 5. CJK legacy fallback.
            return DetectCjkLegacy(data, offset, count);
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
                EncodingKind.ShiftJis => "Shift-JIS",
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

        private static Result DetectCjkLegacy(byte[] data, int offset, int count)
        {
            var sampleCount = Math.Min(count, MaxHeuristicSampleSize);

            var candidates = new[]
            {
                new LegacyCandidate(EncodingKind.Big5, ProbeBig5(data, offset, sampleCount)),
                new LegacyCandidate(EncodingKind.Gb18030, ProbeGb18030(data, offset, sampleCount)),
                new LegacyCandidate(EncodingKind.ShiftJis, ProbeShiftJis(data, offset, sampleCount))
            };

            var best = candidates[0];
            var secondConfidence = 0.0F;

            for (var i = 1; i < candidates.Length; i++)
            {
                var candidate = candidates[i];

                if (candidate.Confidence > best.Confidence)
                {
                    secondConfidence = best.Confidence;
                    best = candidate;
                }
                else if (candidate.Confidence > secondConfidence)
                {
                    secondConfidence = candidate.Confidence;
                }
            }

            if (best.Confidence < MinLegacyConfidence ||
                best.Confidence - secondConfidence < MinLegacyMargin)
            {
                return Unknown();
            }

            return new Result(best.Encoding, 0, best.Confidence);
        }

        private readonly struct LegacyCandidate
        {
            public LegacyCandidate(EncodingKind encoding, float confidence)
            {
                Encoding = encoding;
                Confidence = confidence;
            }

            public EncodingKind Encoding { get; }
            public float Confidence { get; }
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

        private static float ProbeShiftJis(byte[] data, int offset, int count)
        {
            var stateMachine = new CodingStateMachine(
                ShiftJisClassTable,
                6,
                ShiftJisStateTable,
                ShiftJisCharLenTable);

            var distribution = new DistributionAnalysis(
                JisFrequentBits,
                4368,
                3.0F,
                GetShiftJisOrder);

            var context = new JapaneseContextAnalysis();
            var end = offset + count;

            for (var i = offset; i < end; i++)
            {
                var state = stateMachine.NextState(data[i]);

                if (state == StateItsMe)
                    break;

                if (state != StateStart)
                    continue;

                var charLen = stateMachine.CurrentCharLen;
                var charStart = i + 1 - charLen;

                if (charLen == 2 && charStart >= offset)
                {
                    var first = data[charStart];
                    var second = data[charStart + 1];

                    context.HandleOneChar(first, second);
                    distribution.HandleOneChar(first, second);
                }
                else
                {
                    context.HandleNonHiragana();
                }
            }

            return Math.Max(context.GetConfidence(), distribution.GetConfidence());
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

        private sealed class JapaneseContextAnalysis
        {
            private const int MinimumDataThreshold = 4;
            private const int MaxRelationThreshold = 1000;

            private readonly int[] _relationSamples = new int[6];
            private int _totalRelations;
            private int _lastCharOrder = -1;
            private bool _done;

            public void HandleOneChar(byte first, byte second)
            {
                if (_totalRelations > MaxRelationThreshold)
                    _done = true;

                if (_done)
                    return;

                var order = GetShiftJisHiraganaOrder(first, second);

                if (order >= 0 && _lastCharOrder >= 0)
                {
                    _totalRelations++;
                    _relationSamples[JapaneseContext[_lastCharOrder * 83 + order]]++;
                }

                _lastCharOrder = order;
            }

            public void HandleNonHiragana()
            {
                _lastCharOrder = -1;
            }

            public float GetConfidence()
            {
                if (_totalRelations <= MinimumDataThreshold)
                    return -1.0F;

                return (float)(_totalRelations - _relationSamples[0]) / _totalRelations;
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

        private static int GetShiftJisOrder(byte first, byte second)
        {
            int order;

            if (first is >= 0x81 and <= 0x9F)
                order = 188 * (first - 0x81);
            else if (first is >= 0xE0 and <= 0xEF)
                order = 188 * (first - 0xE0 + 31);
            else
                return -1;

            order += second - 0x40;

            if (second > 0x7F)
                order--;

            return order;
        }

        private static int GetShiftJisHiraganaOrder(byte first, byte second)
        {
            return first == 0x82 && second is >= 0x9F and <= 0xF1
                ? second - 0x9F
                : -1;
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

        // These state-machine tables are the unpacked Big5/GB18030/Shift-JIS models
        // from uchardet 0.0.5 nsMBCSSM.cpp.

        private static readonly byte[] ShiftJisClassTable =
        {
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4,
            4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 0, 0, 0,
        };

        private static readonly byte[] ShiftJisStateTable =
        {
            1, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2,
            2, 2, 1, 1, 0, 0, 0, 0,
        };

        private static readonly byte[] ShiftJisCharLenTable =
        {
            0, 1, 1, 2, 0, 0
        };

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

        private static readonly uint[] JisFrequentBits =
        {
            0x1900037Fu, 0x01F86608u, 0x00080000u, 0x00000020u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u,
            0xA8000000u, 0xF7FFFFFAu, 0xBFB6DBFFu, 0xEF001B7Fu, 0xEDD7F5D7u, 0x77FF7DD7u, 0x0000045Fu, 0x00000000u,
            0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x20040000u, 0x00000000u, 0x00000000u, 0x00000000u,
            0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u,
            0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u,
            0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00004080u, 0x30402808u, 0x08002010u, 0x20000020u,
            0x00000000u, 0x28000000u, 0x1F002000u, 0x40000223u, 0x0001C020u, 0x00000840u, 0x80280A02u, 0x00000008u,
            0x00048000u, 0x10828008u, 0x0B200204u, 0x01000050u, 0x00000000u, 0x08602040u, 0x20000414u, 0x00C00000u,
            0x00000402u, 0x00104004u, 0x48000408u, 0x100C0002u, 0x00CA40C0u, 0x20C08000u, 0x0010C900u, 0x10001100u,
            0x00620008u, 0x00012001u, 0x00188000u, 0x08001440u, 0x68000000u, 0x25098000u, 0x0002880Cu, 0x06120722u,
            0xA8000C08u, 0x410B0000u, 0x80020000u, 0x02801010u, 0x41038080u, 0x00180008u, 0x00202100u, 0x04320402u,
            0x04821400u, 0x00012A0Du, 0x00800100u, 0x2240C420u, 0x28000020u, 0x80000209u, 0x000C8210u, 0x04020080u,
            0x00400201u, 0x18210C02u, 0x000A2810u, 0x400000BCu, 0x00801400u, 0x00020760u, 0x40080103u, 0x00842010u,
            0x00000024u, 0x00001008u, 0x1880400Au, 0x40010000u, 0x00032008u, 0x41210062u, 0x00800004u, 0x040A0100u,
            0x00010004u, 0x00004004u, 0x00000020u, 0x01000001u, 0x00020080u, 0x08000010u, 0x02021080u, 0x00040458u,
            0x02810048u, 0x42001060u, 0x02008408u, 0x00001401u, 0x01000001u, 0x00000080u, 0x40001002u, 0x00200380u,
            0x40912028u, 0x04100000u, 0x51102020u, 0xC0208000u, 0x00084010u, 0x00104008u, 0x00000120u, 0x20000020u,
            0x00000048u,
        };

        // Hiragana 2-character sequence frequency categories from JpCntx.cpp.
        // Flattened row-major from the original 83 x 83 table.
        private static readonly byte[] JapaneseContext =
        {
            0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 1, 2, 4, 0, 4, 0, 3, 0, 4, 0, 3, 4, 4, 4,
            2, 4, 3, 3, 4, 3, 2, 3, 3, 4, 2, 3, 3, 3, 2, 4,
            1, 4, 3, 3, 1, 5, 4, 3, 4, 3, 4, 3, 5, 3, 0, 3,
            5, 4, 2, 0, 3, 1, 0, 3, 3, 0, 3, 3, 0, 1, 1, 0,
            4, 3, 0, 3, 3, 0, 4, 0, 2, 0, 3, 5, 5, 5, 5, 4,
            0, 4, 1, 0, 3, 4, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 4, 0, 5, 0, 5, 0,
            4, 0, 4, 5, 4, 4, 3, 5, 3, 5, 1, 5, 3, 4, 3, 4,
            4, 3, 4, 3, 3, 4, 3, 5, 4, 4, 3, 5, 5, 3, 5, 5,
            5, 3, 5, 5, 3, 4, 5, 5, 3, 1, 3, 2, 0, 3, 4, 0,
            4, 2, 0, 4, 2, 1, 5, 3, 2, 3, 5, 0, 4, 0, 2, 0,
            5, 4, 4, 5, 4, 5, 0, 4, 0, 0, 4, 4, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            3, 0, 4, 0, 3, 0, 3, 0, 4, 5, 4, 3, 3, 3, 3, 4,
            3, 5, 4, 4, 3, 5, 4, 4, 3, 4, 3, 4, 4, 4, 4, 5,
            3, 4, 4, 3, 4, 5, 5, 4, 5, 5, 1, 4, 5, 4, 3, 0,
            3, 3, 1, 3, 3, 0, 4, 4, 0, 3, 3, 1, 5, 3, 3, 3,
            5, 0, 4, 0, 3, 0, 4, 4, 3, 4, 3, 3, 0, 4, 1, 1,
            3, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 4, 0, 3, 0, 3, 0, 4, 0, 3, 4,
            4, 3, 2, 2, 1, 2, 1, 3, 1, 3, 3, 3, 3, 3, 4, 3,
            1, 3, 3, 5, 3, 3, 0, 4, 3, 0, 5, 4, 3, 3, 5, 4,
            4, 3, 4, 4, 5, 0, 1, 2, 0, 1, 2, 0, 2, 2, 0, 1,
            0, 0, 5, 2, 2, 1, 4, 0, 3, 0, 1, 0, 4, 4, 3, 5,
            4, 3, 0, 2, 1, 0, 4, 3, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 5, 0,
            4, 0, 2, 1, 4, 4, 2, 4, 1, 4, 2, 4, 2, 4, 3, 3,
            3, 4, 3, 3, 3, 3, 1, 4, 2, 3, 3, 3, 1, 4, 4, 1,
            1, 1, 4, 3, 3, 2, 0, 2, 4, 3, 2, 0, 3, 3, 0, 3,
            1, 1, 0, 0, 0, 3, 3, 0, 4, 2, 2, 3, 4, 0, 4, 0,
            3, 0, 4, 4, 5, 3, 4, 4, 0, 3, 0, 0, 1, 4, 1, 4,
            0, 4, 0, 4, 0, 4, 0, 3, 5, 4, 4, 3, 4, 3, 5, 4,
            3, 3, 4, 3, 5, 4, 4, 4, 4, 3, 4, 2, 4, 3, 3, 1,
            5, 4, 3, 2, 4, 5, 4, 5, 5, 4, 4, 5, 4, 4, 0, 3,
            2, 2, 3, 3, 0, 4, 3, 1, 3, 2, 1, 4, 3, 3, 4, 5,
            0, 3, 0, 2, 0, 4, 5, 5, 4, 5, 4, 0, 4, 0, 0, 5,
            4, 0, 5, 0, 5, 0, 4, 0, 3, 0, 4, 4, 3, 4, 3, 3,
            3, 4, 0, 4, 4, 4, 3, 4, 3, 4, 3, 3, 1, 4, 2, 4,
            3, 4, 0, 5, 4, 1, 4, 5, 4, 4, 5, 3, 2, 4, 3, 4,
            3, 2, 4, 1, 3, 3, 3, 2, 3, 2, 0, 4, 3, 3, 4, 3,
            3, 3, 4, 0, 4, 0, 3, 0, 4, 5, 4, 4, 4, 3, 0, 4,
            1, 0, 1, 3, 0, 3, 1, 4, 0, 3, 0, 2, 0, 3, 4, 4,
            3, 1, 4, 2, 3, 3, 4, 3, 4, 3, 4, 3, 4, 4, 3, 2,
            3, 1, 5, 4, 4, 1, 4, 4, 3, 5, 4, 4, 3, 5, 5, 4,
            3, 4, 4, 3, 1, 2, 3, 1, 2, 2, 0, 3, 2, 0, 3, 1,
            0, 5, 3, 3, 3, 4, 3, 3, 3, 3, 4, 4, 4, 4, 5, 4,
            2, 0, 3, 3, 2, 4, 3, 0, 2, 0, 3, 0, 1, 0, 1, 0,
            0, 3, 2, 0, 0, 2, 0, 1, 0, 2, 1, 3, 3, 3, 1, 2,
            3, 1, 0, 1, 0, 4, 2, 1, 1, 3, 3, 0, 4, 3, 3, 1,
            4, 3, 3, 0, 3, 3, 2, 0, 0, 0, 0, 1, 0, 0, 2, 0,
            0, 0, 0, 0, 4, 1, 0, 2, 3, 2, 2, 2, 1, 3, 3, 3,
            4, 4, 3, 2, 0, 3, 1, 0, 3, 3, 0, 4, 0, 4, 0, 3,
            0, 3, 0, 4, 4, 4, 3, 3, 3, 3, 3, 3, 4, 3, 4, 2,
            4, 3, 4, 3, 3, 2, 4, 3, 4, 5, 4, 1, 4, 5, 3, 5,
            4, 5, 3, 5, 4, 0, 3, 5, 5, 3, 1, 3, 3, 2, 2, 3,
            0, 3, 4, 1, 3, 3, 2, 4, 3, 3, 3, 4, 0, 4, 0, 3,
            0, 4, 5, 4, 4, 5, 3, 0, 4, 1, 0, 3, 4, 0, 2, 0,
            3, 0, 3, 0, 0, 0, 2, 2, 2, 1, 0, 1, 0, 0, 0, 3,
            0, 3, 0, 3, 0, 1, 3, 1, 0, 3, 1, 3, 3, 3, 1, 3,
            3, 3, 0, 1, 3, 1, 3, 4, 0, 0, 3, 1, 1, 0, 3, 2,
            0, 0, 0, 0, 1, 3, 0, 1, 0, 0, 3, 3, 2, 0, 3, 0,
            0, 0, 0, 0, 3, 4, 3, 4, 3, 3, 0, 3, 0, 0, 2, 3,
            2, 3, 0, 3, 0, 2, 0, 1, 0, 3, 3, 4, 3, 1, 3, 1,
            1, 1, 3, 1, 4, 3, 4, 3, 3, 3, 0, 0, 3, 1, 5, 4,
            3, 1, 4, 3, 2, 5, 5, 4, 4, 4, 4, 3, 3, 4, 4, 4,
            0, 2, 1, 1, 3, 2, 0, 1, 2, 0, 0, 1, 0, 4, 1, 3,
            3, 3, 0, 3, 0, 1, 0, 4, 4, 4, 5, 5, 3, 0, 2, 0,
            0, 4, 4, 0, 2, 0, 1, 0, 3, 1, 3, 0, 2, 3, 3, 3,
            0, 3, 1, 0, 0, 3, 0, 3, 2, 3, 1, 3, 2, 1, 1, 0,
            0, 4, 2, 1, 0, 2, 3, 1, 4, 3, 2, 0, 4, 4, 3, 1,
            3, 1, 3, 0, 1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0,
            4, 1, 1, 1, 2, 0, 3, 0, 0, 0, 3, 4, 2, 4, 3, 2,
            0, 1, 0, 0, 3, 3, 0, 1, 0, 4, 0, 5, 0, 4, 0, 2,
            4, 4, 2, 3, 3, 2, 3, 3, 5, 3, 3, 3, 4, 3, 4, 2,
            3, 0, 4, 3, 3, 3, 4, 1, 4, 3, 2, 1, 5, 5, 3, 4,
            5, 1, 3, 5, 4, 2, 0, 3, 3, 0, 1, 3, 0, 4, 2, 0,
            1, 3, 1, 4, 3, 3, 3, 3, 0, 3, 0, 1, 0, 3, 4, 4,
            4, 5, 5, 0, 3, 0, 1, 4, 5, 0, 2, 0, 3, 0, 3, 0,
            0, 0, 2, 3, 1, 3, 0, 4, 0, 1, 1, 3, 0, 3, 4, 3,
            2, 3, 1, 0, 3, 3, 2, 3, 1, 3, 0, 2, 3, 0, 2, 1,
            4, 1, 2, 2, 0, 0, 3, 3, 0, 0, 2, 0, 0, 0, 1, 0,
            0, 0, 0, 2, 2, 0, 3, 2, 1, 3, 3, 0, 2, 0, 2, 0,
            0, 3, 3, 1, 2, 4, 0, 3, 0, 2, 2, 3, 2, 4, 0, 5,
            0, 4, 0, 4, 0, 2, 4, 4, 4, 3, 4, 3, 3, 3, 1, 2,
            4, 3, 4, 3, 4, 4, 5, 0, 3, 3, 3, 3, 2, 0, 4, 3,
            1, 4, 3, 4, 1, 4, 4, 3, 3, 4, 4, 3, 1, 2, 3, 0,
            4, 2, 0, 4, 1, 0, 3, 3, 0, 4, 3, 3, 3, 4, 0, 4,
            0, 2, 0, 3, 5, 3, 4, 5, 2, 0, 3, 0, 0, 4, 5, 0,
            3, 0, 4, 0, 1, 0, 1, 0, 1, 3, 2, 2, 1, 3, 0, 3,
            0, 2, 0, 2, 0, 3, 0, 2, 0, 0, 0, 1, 0, 1, 1, 0,
            0, 3, 1, 0, 0, 0, 4, 0, 3, 1, 0, 2, 1, 3, 0, 0,
            0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 4, 2, 2, 3,
            1, 0, 3, 0, 0, 0, 1, 4, 4, 4, 3, 0, 0, 4, 0, 0,
            1, 4, 1, 4, 1, 5, 0, 3, 0, 3, 0, 4, 5, 4, 4, 3,
            5, 3, 3, 4, 4, 3, 4, 1, 3, 3, 3, 3, 2, 1, 4, 1,
            5, 4, 3, 1, 4, 4, 3, 5, 4, 4, 3, 5, 4, 3, 3, 4,
            4, 4, 0, 3, 3, 1, 2, 3, 0, 3, 1, 0, 3, 3, 0, 5,
            4, 4, 4, 4, 4, 4, 3, 3, 5, 4, 4, 3, 3, 5, 4, 0,
            3, 2, 0, 4, 4, 0, 2, 0, 3, 0, 1, 0, 0, 0, 1, 3,
            3, 3, 2, 4, 1, 3, 0, 3, 1, 3, 0, 2, 2, 1, 1, 0,
            0, 2, 0, 4, 3, 1, 0, 4, 3, 0, 4, 4, 4, 1, 4, 3,
            1, 1, 3, 3, 1, 0, 2, 0, 0, 1, 3, 0, 0, 0, 0, 2,
            0, 0, 4, 3, 2, 4, 3, 5, 4, 3, 3, 3, 4, 3, 3, 4,
            3, 3, 0, 2, 1, 0, 3, 3, 0, 2, 0, 4, 0, 3, 0, 2,
            0, 2, 5, 5, 3, 4, 4, 4, 4, 1, 4, 3, 3, 0, 4, 3,
            4, 3, 1, 3, 3, 2, 4, 3, 0, 3, 4, 3, 0, 3, 4, 4,
            2, 4, 4, 0, 4, 5, 3, 3, 2, 2, 1, 1, 1, 2, 0, 1,
            5, 0, 3, 3, 2, 4, 3, 3, 3, 4, 0, 3, 0, 2, 0, 4,
            4, 3, 5, 5, 0, 0, 3, 0, 2, 3, 3, 0, 3, 0, 4, 0,
            3, 0, 1, 0, 3, 4, 3, 3, 1, 3, 3, 3, 0, 3, 1, 3,
            0, 4, 3, 3, 1, 1, 0, 3, 0, 3, 3, 0, 0, 4, 4, 0,
            1, 5, 4, 3, 3, 5, 0, 3, 3, 4, 3, 0, 2, 0, 1, 1,
            1, 0, 1, 3, 0, 1, 2, 1, 3, 3, 2, 3, 3, 0, 3, 0,
            1, 0, 1, 3, 3, 4, 4, 1, 0, 1, 2, 2, 1, 3, 0, 1,
            0, 4, 0, 4, 0, 3, 0, 1, 3, 3, 3, 2, 3, 1, 1, 0,
            3, 0, 3, 3, 4, 3, 2, 4, 2, 0, 1, 0, 4, 3, 2, 0,
            4, 3, 0, 5, 3, 3, 2, 4, 4, 4, 3, 3, 3, 4, 0, 1,
            3, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 4, 2, 3, 3, 3,
            0, 3, 0, 0, 0, 4, 4, 4, 5, 3, 2, 0, 3, 3, 0, 3,
            5, 0, 2, 0, 3, 0, 0, 0, 3, 0, 1, 3, 0, 2, 0, 0,
            0, 1, 0, 3, 1, 1, 3, 3, 0, 0, 3, 0, 0, 3, 0, 2,
            3, 1, 0, 3, 1, 0, 3, 3, 2, 0, 4, 2, 2, 0, 2, 0,
            0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 1,
            2, 0, 1, 0, 1, 0, 0, 0, 1, 3, 1, 2, 0, 0, 0, 1,
            0, 0, 1, 4, 0, 3, 0, 3, 0, 5, 0, 1, 0, 2, 4, 3,
            1, 3, 3, 2, 1, 1, 5, 2, 1, 0, 5, 1, 2, 0, 0, 0,
            3, 3, 2, 2, 3, 2, 4, 3, 0, 0, 3, 3, 1, 3, 3, 0,
            2, 5, 3, 4, 0, 3, 3, 0, 1, 2, 0, 2, 2, 0, 3, 2,
            0, 2, 2, 3, 3, 3, 0, 2, 0, 1, 0, 3, 4, 4, 2, 5,
            4, 0, 3, 0, 0, 3, 5, 0, 3, 0, 3, 0, 3, 0, 1, 0,
            3, 3, 3, 3, 0, 3, 0, 2, 0, 2, 1, 1, 0, 2, 0, 1,
            0, 0, 0, 2, 1, 0, 0, 1, 0, 3, 2, 0, 0, 3, 3, 1,
            2, 3, 1, 0, 3, 3, 0, 0, 1, 0, 0, 0, 0, 0, 2, 0,
            0, 0, 0, 0, 2, 3, 1, 2, 3, 0, 3, 0, 1, 0, 3, 2,
            1, 0, 4, 3, 0, 1, 1, 0, 3, 3, 0, 4, 0, 5, 0, 3,
            0, 3, 0, 4, 5, 5, 4, 3, 5, 3, 4, 3, 5, 3, 3, 2,
            5, 3, 4, 4, 4, 3, 4, 3, 4, 5, 5, 3, 4, 4, 3, 4,
            4, 5, 4, 4, 4, 3, 4, 5, 5, 4, 2, 3, 4, 2, 3, 4,
            0, 3, 3, 1, 4, 3, 2, 4, 3, 3, 5, 5, 0, 3, 0, 3,
            0, 5, 5, 5, 5, 4, 4, 0, 4, 0, 1, 4, 4, 0, 4, 0,
            4, 0, 3, 0, 3, 0, 3, 5, 4, 4, 2, 3, 2, 5, 1, 3,
            2, 5, 1, 4, 2, 3, 2, 3, 3, 4, 3, 3, 3, 3, 2, 5,
            4, 1, 3, 3, 5, 3, 4, 4, 0, 4, 4, 3, 1, 1, 3, 1,
            0, 2, 3, 0, 2, 3, 0, 3, 0, 0, 4, 3, 1, 3, 4, 0,
            3, 0, 2, 0, 4, 4, 4, 3, 4, 5, 0, 4, 0, 0, 3, 4,
            0, 3, 0, 3, 0, 3, 1, 2, 0, 3, 4, 4, 3, 3, 3, 0,
            2, 2, 4, 3, 3, 1, 3, 3, 3, 1, 1, 0, 3, 1, 4, 3,
            2, 3, 4, 4, 2, 4, 4, 4, 3, 4, 4, 3, 2, 4, 4, 3,
            1, 3, 3, 1, 3, 3, 0, 4, 1, 0, 2, 2, 1, 4, 3, 2,
            3, 3, 5, 4, 3, 3, 5, 4, 4, 3, 3, 0, 4, 0, 3, 2,
            2, 4, 4, 0, 2, 0, 1, 0, 0, 0, 0, 0, 1, 2, 1, 3,
            0, 0, 0, 0, 0, 2, 0, 1, 2, 1, 0, 0, 1, 0, 0, 0,
            0, 3, 0, 0, 1, 0, 1, 1, 3, 1, 0, 0, 0, 1, 1, 0,
            1, 1, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 2, 2, 0, 3, 4, 0, 0, 0, 1, 1, 0, 0, 1, 0,
            0, 0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0,
            4, 0, 4, 1, 4, 0, 3, 0, 4, 0, 3, 0, 4, 0, 3, 0,
            3, 0, 4, 1, 5, 1, 4, 0, 0, 3, 0, 5, 0, 5, 2, 0,
            1, 0, 0, 0, 2, 1, 4, 0, 1, 3, 0, 0, 3, 0, 0, 3,
            1, 1, 4, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 4, 0, 5, 0, 3, 0,
            2, 0, 3, 5, 4, 4, 3, 4, 3, 5, 3, 4, 3, 3, 0, 4,
            3, 3, 3, 3, 3, 3, 2, 4, 4, 3, 1, 3, 4, 4, 5, 4,
            4, 3, 4, 4, 1, 3, 5, 4, 3, 3, 3, 1, 2, 2, 3, 3,
            1, 3, 1, 3, 3, 3, 5, 3, 3, 4, 5, 0, 3, 0, 3, 0,
            3, 4, 3, 4, 4, 3, 0, 3, 0, 2, 4, 3, 0, 1, 0, 4,
            0, 0, 0, 0, 0, 1, 4, 0, 4, 1, 4, 2, 4, 0, 3, 0,
            1, 0, 1, 0, 0, 0, 0, 0, 2, 0, 3, 1, 1, 1, 0, 3,
            0, 0, 0, 1, 2, 1, 0, 0, 1, 1, 1, 1, 0, 1, 0, 0,
            0, 1, 0, 0, 3, 0, 0, 0, 0, 3, 2, 0, 2, 2, 0, 1,
            0, 0, 0, 2, 3, 2, 3, 3, 0, 0, 0, 0, 2, 1, 0, 0,
            5, 1, 5, 0, 3, 0, 3, 0, 5, 4, 4, 5, 1, 5, 3, 3,
            0, 4, 3, 4, 3, 5, 3, 4, 3, 3, 2, 4, 3, 4, 3, 3,
            0, 3, 3, 1, 4, 4, 3, 4, 4, 4, 3, 4, 5, 5, 3, 2,
            3, 1, 1, 3, 3, 1, 3, 1, 1, 3, 3, 2, 4, 5, 3, 3,
            5, 0, 4, 0, 3, 0, 4, 4, 3, 5, 3, 3, 0, 3, 4, 0,
            4, 3, 0, 5, 0, 5, 0, 3, 0, 2, 0, 4, 4, 3, 5, 2,
            4, 3, 3, 3, 4, 4, 4, 3, 5, 3, 5, 3, 3, 1, 4, 0,
            4, 3, 3, 0, 3, 3, 0, 4, 4, 4, 4, 5, 4, 3, 3, 5,
            5, 3, 2, 3, 1, 2, 3, 2, 0, 1, 0, 0, 3, 2, 2, 4,
            4, 3, 1, 5, 0, 4, 0, 3, 0, 4, 3, 1, 3, 2, 1, 0,
            3, 3, 0, 3, 3, 0, 4, 0, 5, 0, 5, 0, 4, 0, 4, 5,
            5, 5, 3, 4, 3, 3, 2, 5, 4, 4, 3, 5, 3, 5, 3, 4,
            0, 4, 3, 4, 4, 3, 2, 4, 4, 3, 4, 5, 4, 4, 5, 5,
            0, 3, 5, 5, 4, 1, 3, 3, 2, 3, 3, 1, 3, 1, 0, 4,
            3, 1, 4, 4, 3, 4, 5, 0, 4, 0, 2, 0, 4, 3, 4, 4,
            3, 3, 0, 4, 0, 0, 5, 5, 0, 4, 0, 4, 0, 5, 0, 1,
            1, 3, 3, 4, 4, 3, 4, 1, 3, 0, 5, 1, 3, 0, 3, 1,
            3, 1, 1, 0, 3, 0, 3, 3, 4, 0, 4, 3, 0, 4, 4, 4,
            3, 4, 4, 0, 3, 5, 4, 1, 0, 3, 0, 0, 2, 3, 0, 3,
            1, 0, 3, 1, 0, 3, 2, 1, 3, 5, 0, 3, 0, 1, 0, 3,
            2, 3, 3, 4, 4, 0, 2, 2, 0, 4, 4, 2, 4, 0, 5, 0,
            4, 0, 3, 0, 4, 5, 5, 4, 3, 5, 3, 5, 3, 5, 3, 5,
            2, 5, 3, 4, 3, 3, 4, 3, 4, 5, 3, 2, 1, 5, 4, 3,
            2, 3, 4, 5, 3, 4, 1, 2, 5, 4, 3, 0, 3, 3, 0, 3,
            2, 0, 2, 3, 0, 4, 1, 0, 3, 4, 3, 3, 5, 0, 3, 0,
            1, 0, 4, 5, 5, 5, 4, 3, 0, 4, 2, 0, 3, 5, 0, 5,
            0, 4, 0, 4, 0, 2, 0, 5, 4, 3, 4, 3, 4, 3, 3, 3,
            4, 3, 4, 2, 5, 3, 5, 3, 4, 1, 4, 3, 4, 4, 4, 0,
            3, 5, 0, 4, 4, 4, 4, 5, 3, 1, 3, 4, 5, 3, 3, 3,
            3, 3, 3, 3, 0, 2, 2, 0, 3, 3, 2, 4, 3, 3, 3, 5,
            3, 4, 1, 3, 3, 5, 3, 2, 0, 0, 0, 0, 4, 3, 1, 3,
            3, 0, 1, 0, 3, 0, 3, 0, 1, 0, 1, 3, 3, 3, 2, 3,
            3, 3, 0, 3, 0, 0, 0, 3, 1, 3, 0, 0, 0, 2, 2, 2,
            3, 0, 0, 3, 2, 0, 1, 2, 4, 1, 3, 3, 0, 0, 3, 3,
            3, 0, 1, 0, 0, 2, 1, 0, 0, 3, 0, 3, 1, 0, 3, 0,
            0, 1, 3, 0, 2, 0, 1, 0, 3, 3, 1, 3, 3, 0, 0, 1,
            1, 0, 3, 3, 0, 2, 0, 3, 0, 2, 1, 4, 0, 2, 2, 3,
            1, 1, 3, 1, 1, 0, 2, 0, 3, 1, 2, 3, 1, 3, 0, 0,
            1, 0, 4, 3, 2, 3, 3, 3, 1, 4, 2, 3, 3, 3, 3, 1,
            0, 3, 1, 4, 0, 1, 1, 0, 1, 2, 0, 1, 1, 0, 1, 1,
            0, 3, 1, 3, 2, 2, 0, 1, 0, 0, 0, 2, 3, 3, 3, 1,
            0, 0, 0, 0, 0, 2, 3, 0, 5, 0, 4, 0, 5, 0, 2, 0,
            4, 5, 5, 3, 3, 4, 3, 3, 1, 5, 4, 4, 2, 4, 4, 4,
            3, 4, 2, 4, 3, 5, 5, 4, 3, 3, 4, 3, 3, 5, 5, 4,
            5, 5, 1, 3, 4, 5, 3, 1, 4, 3, 1, 3, 3, 0, 3, 3,
            1, 4, 3, 1, 4, 5, 3, 3, 5, 0, 4, 0, 3, 0, 5, 3,
            3, 1, 4, 3, 0, 4, 0, 1, 5, 3, 0, 5, 0, 5, 0, 4,
            0, 2, 0, 4, 4, 3, 4, 3, 3, 3, 3, 3, 5, 4, 4, 4,
            4, 4, 4, 5, 3, 3, 5, 2, 4, 4, 4, 3, 4, 4, 3, 3,
            4, 4, 5, 5, 3, 3, 4, 3, 4, 3, 3, 4, 3, 3, 3, 3,
            1, 2, 2, 1, 4, 3, 3, 5, 4, 4, 3, 4, 0, 4, 0, 3,
            0, 4, 4, 4, 4, 4, 1, 0, 4, 2, 0, 2, 4, 0, 4, 0,
            4, 0, 3, 0, 1, 0, 3, 5, 2, 3, 0, 3, 0, 2, 1, 4,
            2, 3, 3, 4, 1, 4, 3, 3, 2, 4, 1, 3, 3, 3, 0, 3,
            3, 0, 0, 3, 3, 3, 5, 3, 3, 3, 3, 3, 2, 0, 2, 0,
            0, 2, 0, 0, 2, 0, 0, 1, 0, 0, 3, 1, 2, 2, 3, 0,
            3, 0, 2, 0, 4, 4, 3, 3, 4, 1, 0, 3, 0, 0, 2, 4,
            0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 2, 0,
            0, 0, 0, 0, 1, 0, 2, 0, 1, 0, 0, 0, 0, 0, 3, 1,
            3, 0, 3, 2, 0, 0, 0, 1, 0, 3, 2, 0, 0, 2, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 3, 4, 0, 2, 0, 0, 0, 0,
            0, 0, 2, 0, 2, 1, 3, 0, 2, 0, 2, 0, 3, 3, 3, 3,
            1, 3, 1, 3, 3, 3, 3, 3, 3, 4, 2, 2, 1, 2, 1, 4,
            0, 4, 3, 1, 3, 3, 3, 2, 4, 3, 5, 4, 3, 3, 3, 3,
            3, 3, 3, 0, 1, 3, 0, 2, 0, 0, 1, 0, 0, 1, 0, 0,
            4, 2, 0, 2, 3, 0, 3, 3, 0, 3, 3, 4, 2, 3, 1, 4,
            0, 1, 2, 0, 2, 3, 0, 3, 0, 3, 0, 1, 0, 3, 0, 2,
            3, 3, 3, 0, 3, 1, 2, 0, 3, 3, 2, 3, 3, 2, 3, 2,
            3, 1, 3, 0, 4, 3, 2, 0, 3, 3, 1, 4, 3, 3, 2, 3,
            4, 3, 1, 3, 3, 1, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1,
            0, 0, 0, 4, 1, 1, 0, 3, 0, 3, 1, 0, 2, 3, 3, 3,
            3, 3, 1, 0, 0, 2, 0, 3, 3, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 3, 0, 2, 0, 3, 0, 0, 0, 0, 0, 0, 0, 3,
            0, 0, 0, 0, 0, 0, 0, 3, 0, 3, 0, 3, 1, 0, 1, 0,
            1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 2,
            0, 2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 2, 0, 3,
            1, 3, 0, 3, 0, 2, 3, 3, 3, 1, 3, 1, 3, 1, 3, 1,
            3, 3, 3, 1, 3, 0, 2, 3, 1, 1, 4, 3, 3, 2, 3, 3,
            1, 2, 2, 4, 1, 3, 3, 0, 1, 4, 2, 3, 0, 1, 3, 0,
            3, 0, 0, 1, 3, 0, 2, 0, 0, 3, 3, 2, 1, 3, 0, 3,
            0, 2, 0, 3, 4, 4, 4, 3, 1, 0, 3, 0, 0, 3, 3, 0,
            2, 0, 1, 0, 2, 0, 0, 0, 1, 3, 2, 2, 1, 3, 0, 1,
            1, 3, 0, 3, 2, 3, 1, 2, 0, 2, 0, 1, 1, 3, 3, 3,
            0, 3, 3, 1, 1, 2, 3, 2, 3, 3, 1, 2, 3, 2, 0, 0,
            1, 0, 0, 0, 0, 0, 0, 3, 0, 1, 0, 0, 2, 1, 2, 1,
            3, 0, 3, 0, 0, 0, 3, 4, 4, 4, 3, 2, 0, 2, 0, 0,
            2, 4, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0,
            1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 2, 2, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 3, 1, 0, 0, 0,
            0, 0, 0, 0, 3, 0, 3, 0, 3, 0, 2, 0, 3, 0, 3, 3,
            3, 2, 3, 2, 2, 2, 0, 3, 1, 3, 3, 3, 2, 3, 3, 0,
            0, 3, 0, 3, 2, 2, 0, 2, 3, 1, 4, 3, 4, 3, 3, 2,
            3, 1, 5, 4, 4, 0, 3, 1, 2, 1, 3, 0, 3, 1, 1, 2,
            0, 2, 3, 1, 3, 1, 3, 0, 3, 0, 1, 0, 3, 3, 4, 4,
            2, 1, 0, 2, 1, 0, 2, 4, 0, 1, 0, 3, 0, 1, 0, 2,
            0, 1, 4, 2, 5, 1, 4, 0, 2, 0, 2, 1, 3, 1, 4, 0,
            2, 1, 0, 0, 2, 1, 4, 1, 1, 0, 3, 3, 0, 5, 1, 3,
            2, 3, 3, 1, 0, 3, 2, 3, 0, 1, 0, 0, 0, 0, 0, 0,
            1, 0, 0, 0, 0, 4, 0, 1, 0, 3, 0, 2, 0, 1, 0, 3,
            3, 3, 4, 3, 3, 0, 0, 0, 0, 2, 3, 0, 0, 0, 1, 0,
            0, 0, 0, 0, 0, 2, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 2, 1, 0, 0, 1, 0, 0, 0, 0, 0, 3, 0, 1,
            0, 3, 0, 4, 0, 3, 0, 2, 4, 3, 1, 0, 3, 2, 2, 1,
            3, 1, 2, 2, 3, 1, 1, 1, 2, 1, 3, 0, 1, 2, 0, 1,
            3, 2, 1, 3, 0, 5, 5, 1, 0, 0, 1, 3, 2, 1, 0, 3,
            0, 0, 1, 0, 0, 0, 0, 0, 3, 4, 0, 1, 1, 1, 3, 2,
            0, 2, 0, 1, 0, 2, 3, 3, 1, 2, 3, 0, 1, 0, 1, 0,
            4, 0, 0, 0, 1, 0, 3, 0, 3, 0, 2, 2, 1, 0, 0, 4,
            0, 3, 0, 3, 1, 3, 0, 3, 0, 3, 0, 1, 0, 3, 0, 3,
            1, 3, 0, 3, 3, 0, 0, 1, 2, 1, 1, 1, 0, 1, 2, 0,
            0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2,
            1, 2, 0, 0, 2, 0, 0, 0, 0, 2, 3, 3, 3, 3, 0, 0,
            0, 0, 1, 4, 0, 0, 0, 3, 0, 3, 0, 0, 0, 0, 3, 1,
            1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 0, 0, 0, 0, 0, 0,
            1, 0, 3, 0, 2, 0, 2, 3, 0, 0, 2, 2, 3, 1, 2, 0,
            0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0,
            2, 0, 0, 0, 0, 2, 3, 2, 4, 0, 5, 0, 5, 0, 4, 0,
            3, 4, 3, 3, 3, 4, 3, 3, 3, 4, 3, 4, 4, 5, 4, 5,
            5, 5, 2, 3, 0, 5, 5, 4, 1, 5, 4, 3, 1, 5, 4, 3,
            4, 4, 3, 3, 4, 3, 3, 0, 3, 2, 0, 2, 3, 0, 3, 0,
            0, 3, 3, 0, 5, 3, 2, 3, 3, 0, 3, 0, 3, 0, 3, 4,
            5, 4, 5, 3, 0, 4, 3, 0, 3, 4, 0, 3, 0, 3, 0, 3,
            0, 3, 0, 3, 3, 4, 3, 2, 3, 2, 3, 0, 4, 3, 3, 3,
            3, 3, 3, 3, 3, 0, 3, 2, 4, 3, 3, 1, 3, 4, 3, 4,
            4, 4, 3, 4, 4, 3, 2, 4, 4, 1, 0, 2, 0, 0, 1, 1,
            0, 2, 0, 0, 3, 1, 0, 5, 3, 2, 1, 3, 0, 3, 0, 1,
            2, 4, 3, 2, 4, 3, 3, 0, 3, 2, 0, 4, 4, 0, 3, 0,
            3, 0, 1, 0, 0, 0, 1, 4, 3, 3, 2, 3, 1, 3, 1, 4,
            2, 3, 2, 4, 2, 3, 4, 3, 0, 2, 2, 3, 3, 3, 0, 3,
            3, 3, 0, 3, 4, 1, 3, 3, 0, 3, 4, 3, 3, 0, 1, 1,
            0, 1, 0, 0, 0, 4, 0, 3, 0, 0, 3, 1, 2, 1, 3, 0,
            4, 0, 1, 0, 4, 3, 3, 4, 3, 3, 0, 2, 0, 0, 3, 3,
            0, 3, 0, 4, 0, 1, 0, 3, 0, 3, 4, 3, 3, 0, 3, 3,
            3, 1, 3, 1, 3, 3, 4, 3, 3, 3, 0, 0, 3, 1, 5, 3,
            3, 1, 3, 3, 2, 5, 4, 3, 3, 4, 5, 3, 2, 5, 3, 4,
            0, 1, 0, 0, 0, 0, 0, 2, 0, 0, 1, 1, 0, 4, 2, 2,
            1, 3, 0, 3, 0, 2, 0, 4, 4, 3, 5, 3, 2, 0, 1, 1,
            0, 3, 4, 0, 5, 0, 4, 0, 5, 0, 2, 0, 4, 4, 3, 3,
            2, 3, 3, 3, 1, 4, 3, 4, 1, 5, 3, 4, 3, 4, 0, 4,
            2, 4, 3, 4, 1, 5, 4, 0, 4, 4, 4, 4, 5, 4, 1, 3,
            5, 4, 2, 1, 4, 1, 1, 3, 2, 0, 3, 1, 0, 3, 2, 1,
            4, 3, 3, 3, 4, 0, 4, 0, 3, 0, 4, 4, 4, 3, 3, 3,
            0, 4, 2, 0, 3, 4, 1, 4, 0, 4, 0, 3, 0, 1, 0, 3,
            3, 3, 1, 1, 3, 3, 2, 2, 3, 3, 1, 0, 3, 2, 2, 1,
            2, 0, 3, 1, 2, 1, 2, 0, 3, 2, 0, 2, 2, 3, 3, 4,
            3, 0, 3, 3, 1, 2, 0, 1, 1, 3, 1, 2, 0, 0, 3, 0,
            1, 1, 0, 3, 2, 2, 3, 3, 0, 3, 0, 0, 0, 2, 3, 3,
            4, 3, 3, 0, 1, 0, 0, 1, 4, 0, 4, 0, 4, 0, 4, 0,
            0, 0, 3, 4, 4, 3, 1, 4, 2, 3, 2, 3, 3, 3, 1, 4,
            3, 4, 0, 3, 0, 4, 2, 3, 3, 2, 2, 5, 4, 2, 1, 3,
            4, 3, 4, 3, 1, 3, 3, 4, 2, 0, 2, 1, 0, 3, 3, 0,
            0, 2, 0, 3, 1, 0, 4, 4, 3, 4, 3, 0, 4, 0, 1, 0,
            2, 4, 4, 4, 4, 4, 0, 3, 2, 0, 3, 3, 0, 0, 0, 1,
            0, 4, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 3, 2,
            0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 2, 0,
            2, 0, 3, 0, 4, 0, 4, 0, 1, 3, 3, 3, 0, 4, 0, 2,
            1, 2, 1, 1, 1, 2, 0, 3, 1, 1, 0, 1, 0, 3, 1, 0,
            0, 3, 3, 2, 0, 1, 1, 0, 0, 0, 0, 0, 1, 0, 2, 0,
            2, 2, 0, 3, 1, 0, 0, 1, 0, 1, 1, 0, 1, 2, 0, 3,
            0, 0, 0, 0, 1, 0, 0, 3, 3, 4, 3, 1, 0, 1, 0, 3,
            0, 2, 0, 0, 0, 3, 0, 5, 0, 0, 0, 0, 1, 0, 2, 0,
            3, 1, 0, 1, 3, 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0,
            1, 1, 0, 0, 4, 0, 0, 0, 2, 3, 0, 1, 4, 1, 0, 2,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0,
            0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 3, 0,
            0, 0, 0, 0, 3, 0, 2, 0, 5, 0, 5, 0, 1, 0, 2, 4,
            3, 3, 2, 5, 1, 3, 2, 3, 3, 3, 0, 4, 1, 2, 0, 3,
            0, 4, 0, 2, 2, 1, 1, 5, 3, 0, 0, 1, 4, 2, 3, 2,
            0, 3, 3, 3, 2, 0, 2, 4, 1, 1, 2, 0, 1, 1, 0, 3,
            1, 0, 1, 3, 1, 2, 3, 0, 2, 0, 0, 0, 1, 3, 5, 4,
            4, 4, 0, 3, 0, 0, 1, 3, 0, 4, 0, 5, 0, 4, 0, 4,
            0, 4, 5, 4, 3, 3, 4, 3, 3, 3, 4, 3, 4, 4, 5, 3,
            4, 5, 4, 2, 4, 2, 3, 4, 3, 1, 4, 4, 1, 3, 5, 4,
            4, 5, 5, 4, 4, 5, 5, 5, 2, 3, 3, 1, 4, 3, 1, 3,
            3, 0, 3, 3, 1, 4, 3, 4, 4, 4, 0, 3, 0, 4, 0, 3,
            3, 4, 4, 5, 0, 0, 4, 3, 0, 4, 5, 0, 4, 0, 4, 0,
            3, 0, 3, 0, 3, 4, 4, 4, 3, 3, 2, 4, 3, 4, 3, 4,
            3, 5, 3, 4, 3, 2, 1, 4, 2, 4, 4, 3, 1, 3, 4, 2,
            4, 5, 5, 3, 4, 5, 4, 1, 5, 4, 3, 0, 3, 2, 2, 3,
            2, 1, 3, 1, 0, 3, 3, 3, 5, 3, 3, 3, 5, 4, 4, 2,
            3, 3, 4, 3, 3, 3, 2, 1, 0, 3, 2, 1, 4, 3, 0, 4,
            0, 5, 0, 4, 0, 3, 0, 3, 5, 5, 3, 2, 4, 3, 4, 0,
            5, 4, 4, 1, 4, 4, 4, 3, 3, 3, 4, 3, 5, 5, 2, 3,
            3, 4, 1, 2, 5, 5, 3, 5, 5, 2, 3, 5, 5, 4, 0, 3,
            2, 0, 3, 3, 1, 1, 5, 1, 4, 1, 0, 4, 3, 2, 3, 5,
            0, 4, 0, 3, 0, 5, 4, 3, 4, 3, 0, 0, 4, 1, 0, 4,
            4, 1, 3, 0, 4, 0, 2, 0, 2, 0, 2, 5, 5, 3, 3, 3,
            3, 3, 0, 4, 2, 3, 4, 4, 4, 3, 4, 0, 0, 3, 4, 5,
            4, 3, 3, 3, 3, 2, 5, 5, 4, 5, 5, 5, 4, 3, 5, 5,
            5, 1, 3, 1, 0, 1, 0, 0, 3, 2, 0, 4, 2, 0, 5, 2,
            3, 2, 4, 1, 3, 0, 3, 0, 4, 5, 4, 5, 4, 3, 0, 4,
            2, 0, 5, 4, 0, 3, 0, 4, 0, 5, 0, 3, 0, 3, 4, 4,
            3, 2, 3, 2, 3, 3, 3, 3, 3, 2, 4, 3, 3, 2, 2, 0,
            3, 3, 3, 3, 3, 1, 3, 3, 3, 0, 4, 4, 3, 4, 4, 1,
            1, 4, 4, 2, 0, 3, 1, 0, 1, 1, 0, 4, 1, 0, 2, 3,
            1, 3, 3, 1, 3, 4, 0, 3, 0, 1, 0, 3, 1, 3, 0, 0,
            1, 0, 2, 0, 0, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 3, 0, 2,
            0, 3, 0, 1, 5, 4, 3, 3, 3, 1, 4, 2, 1, 2, 3, 4,
            4, 2, 4, 4, 5, 0, 3, 1, 4, 3, 4, 0, 4, 3, 3, 3,
            2, 3, 2, 5, 3, 4, 3, 2, 2, 3, 0, 0, 3, 0, 2, 1,
            0, 1, 2, 0, 0, 0, 0, 2, 1, 1, 3, 1, 0, 2, 0, 4,
            0, 3, 4, 4, 4, 5, 2, 0, 2, 0, 0, 1, 3, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0,
            0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 4, 2, 1, 1, 0,
            1, 0, 3, 2, 0, 0, 3, 1, 1, 1, 2, 2, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 1, 0, 0, 0,
            2, 0, 0, 0, 1, 4, 0, 4, 2, 1, 0, 0, 0, 0, 0, 1,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0,
            1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 3, 1,
            0, 0, 0, 2, 0, 2, 1, 0, 0, 1, 2, 1, 0, 1, 1, 0,
            0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 3, 1,
            0, 0, 0, 0, 0, 1, 0, 0, 2, 1, 0, 0, 0, 0, 0, 0,
            0, 0, 2, 0, 4, 0, 4, 0, 4, 0, 3, 0, 4, 4, 3, 4,
            2, 4, 3, 2, 0, 4, 4, 4, 3, 5, 3, 5, 3, 3, 2, 4,
            2, 4, 3, 4, 3, 1, 4, 0, 2, 3, 4, 4, 4, 3, 3, 3,
            4, 4, 4, 3, 4, 1, 3, 4, 3, 2, 1, 2, 1, 3, 3, 3,
            4, 4, 3, 3, 5, 0, 4, 0, 3, 0, 4, 3, 3, 3, 2, 1,
            0, 3, 0, 0, 3, 3, 0, 4, 0, 3, 0, 3, 0, 3, 0, 3,
            5, 5, 3, 3, 3, 3, 4, 3, 4, 3, 3, 3, 4, 4, 4, 3,
            3, 3, 3, 4, 3, 5, 3, 3, 1, 3, 2, 4, 5, 5, 5, 5,
            4, 3, 4, 5, 5, 3, 2, 2, 3, 3, 3, 3, 2, 3, 3, 1,
            2, 3, 2, 4, 3, 3, 3, 4, 0, 4, 0, 2, 0, 4, 3, 2,
            2, 1, 2, 0, 3, 0, 0, 4, 1,
        };

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