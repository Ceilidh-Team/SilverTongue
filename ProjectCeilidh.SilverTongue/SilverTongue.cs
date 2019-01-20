using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProjectCeilidh.SilverTongue
{
    /// <summary>
    /// An I18n helper library based on Airbnb's Polyglot.js (https://github.com/airbnb/polyglot.js).
    /// </summary>
    public sealed class SilverTongue
    {
        /// <summary>
        /// Maps cultures to their plural group.
        /// </summary>
        private static readonly IReadOnlyDictionary<CultureInfo, PluralGroup> CultureGroups =
            new Dictionary<PluralGroup, string[]>
                {
                    [PluralGroup.Arabic] = new[] {"ar"},
                    [PluralGroup.BosnianSerbian] = new[] {"bs-Latn-BA", "bs-Cyrl-BA", "srl-RS", "sr-RS"},
                    [PluralGroup.Chinese] = new[] {"id", "id-ID", "ja", "ko", "ko-KR", "lo", "ms", "th", "th-TH", "zh"},
                    [PluralGroup.Croatian] = new[] {"hr", "hr-HR"},
                    [PluralGroup.German] = new[]
                    {
                        "", "fa", "da", "de", "en", "es", "fi", "el", "he", "hi-IN", "hu", "hu-HU", "it", "nl", "no",
                        "pt",
                        "sv", "tr"
                    }, // Note: The invariant culture is English, but not region-specific.
                    [PluralGroup.French] = new[] {"fr", "tl", "pt-br"},
                    [PluralGroup.Russian] = new[] {"ru", "ru-RU"},
                    [PluralGroup.Lithuanian] = new[] {"lt"},
                    [PluralGroup.Czech] = new[] {"cs", "cs-CZ", "sk"},
                    [PluralGroup.Polish] = new[] {"pl"},
                    [PluralGroup.Icelandic] = new[] {"is"},
                    [PluralGroup.Slovenian] = new[] {"sl-SL"},
                }.SelectMany(x => x.Value.Select(y => (culture: CultureInfo.GetCultureInfo(y), pluralGroup: x.Key)))
                .ToDictionary(x => x.culture, y => y.pluralGroup);
        
        /// <summary>
        /// The culture used for pluralization.
        /// </summary>
        public CultureInfo Culture { get; }
        /// <summary>
        /// The current phrase set.
        /// </summary>
        public IReadOnlyDictionary<string, string[]> Phrases => _phrases;

        private readonly ConcurrentDictionary<string, string[]> _phrases;

        /// <summary>
        /// Construct an instance of <see cref="SilverTongue"/> using the current UI culture and an empty phrase bank.
        /// </summary>
        public SilverTongue() : this(CultureInfo.CurrentUICulture) { }
        
        /// <summary>
        /// Construct an instance of <see cref="SilverTongue"/> using the specified culture and an empty phrase bank.
        /// </summary>
        /// <param name="culture">The culture to use for pluralization.</param>
        public SilverTongue(CultureInfo culture)
        {
            Culture = culture;
            _phrases = new ConcurrentDictionary<string, string[]>();
        }

        /// <summary>
        /// Construct an instance of <see cref="SilverTongue"/> using the specified culture and initial phrase bank.
        /// </summary>
        /// <param name="culture">The culture to use for pluralization.</param>
        /// <param name="phrases">The initial phrases used to populate the phrase bank.</param>
        public SilverTongue(CultureInfo culture, IEnumerable<KeyValuePair<string, string[]>> phrases) : this(culture)
        {
            Extend(phrases);
        }

        /// <summary>
        /// Add new phrases to the phrase bank.
        /// </summary>
        /// <param name="phrases">The phrases to be added to the bank.</param>
        /// <exception cref="ArgumentNullException"><paramref name="phrases"/> was null.</exception>
        /// <exception cref="ArgumentException">A 0-length phrase was found in the input.</exception>
        public void Extend(IEnumerable<KeyValuePair<string, string[]>> phrases)
        {
            if (phrases == null) throw new ArgumentNullException(nameof(phrases));

            foreach (var (key, phrase) in phrases)
            {
                if (phrase == null || phrase.Length <= 0) throw new ArgumentException();
                
                _phrases[key] = phrase;
            }
        }

        /// <summary>
        /// Remove the specified keys from the phrase bank.
        /// </summary>
        /// <param name="keys">The keys to remove from the bank.</param>
        public void Unset(params string[] keys)
        {
            foreach (var key in keys)
                _phrases.TryRemove(key, out _);
        }

        /// <summary>
        /// Clear out the phrase bank.
        /// </summary>
        public void Clear() => _phrases.Clear();

        /// <summary>
        /// Replace the contents of the phrase bank.
        /// </summary>
        /// <param name="phrases">The new initial bank contents.</param>
        public void Replace(IEnumerable<KeyValuePair<string, string[]>> phrases)
        {
            Clear();
            Extend(phrases);
        }

        /// <summary>
        /// Create a translation of the given key with the specified interpolation arguments.
        /// </summary>
        /// <param name="key">The key of the phrase to translate.</param>
        /// <param name="args">The arguments to use when interpolating.</param>
        /// <returns>The translated string.</returns>
        public string Translate(string key, params object[] args)
        {
            if (!_phrases.TryGetValue(key, out var phrase)) return key;

            string specific;

            if (args.Length > 0 && IsIntegral(args[0], out var count)) // Apply smart-pluralization
            {
                var idx = PluralPhraseIndex(Culture, count);
                specific = idx >= phrase.Length ? phrase[0] : phrase[idx];
            }
            else specific = phrase[0];

            try
            {
                return string.Format(specific, args);
            }
            catch (FormatException)
            {
                return specific;
            }
        }

        /// <summary>
        /// Determine if the specified value is of an integer type.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <param name="num">The value as a number.</param>
        /// <returns>True if the value was a number, false otherwise.</returns>
        private static bool IsIntegral(object value, out decimal num)
        {
            num = default;
            if (value == null) return false;

            var typ = value.GetType();

            var nTyp = Nullable.GetUnderlyingType(typ);
            if (nTyp != null) typ = nTyp;

            switch (Type.GetTypeCode(typ))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    num = Convert.ToDecimal(value);
                    return true;
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Produce the index into the phrase array to use for the specified count.
        /// </summary>
        /// <param name="culture">The culture to use when computing the index.</param>
        /// <param name="count">The count to be inserted into the phrase.</param>
        /// <returns>The index into the phrase array to use.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if an invalid plural group is encountered.</exception>
        private static int PluralPhraseIndex(CultureInfo culture, decimal count)
        {
            if (!CultureGroups.TryGetValue(culture, out var group) &&
                !CultureGroups.TryGetValue(CultureInfo.GetCultureInfo(culture.TwoLetterISOLanguageName), out group))
                group = CultureGroups[CultureInfo.InvariantCulture];

            var lastTwo = count % 100;
            var end = count % 10;

            switch (group)
            {
                case PluralGroup.Arabic:
                    if (count < 3) return (int)count;
                    if (lastTwo >= 3 && lastTwo <= 10) return 3;
                    return lastTwo >= 11 ? 4 : 5;
                case PluralGroup.BosnianSerbian:
                case PluralGroup.Croatian:
                case PluralGroup.Russian:
                    if (count != 11 && end == 1)
                        return 0;

                    if (2 <= end && end <= 4 && !(12 <= count && count <= 14))
                        return 1;

                    return 2;
                case PluralGroup.Chinese:
                    return 0;
                case PluralGroup.French:
                    return count > 1 ? 1 : 0;
                case PluralGroup.German:
                    return count != 1 ? 1 : 0;
                case PluralGroup.Lithuanian:
                    if (end == 1 && lastTwo != 11) return 0;
                    return end >= 2 && end <= 9 && (lastTwo < 11 || lastTwo > 19) ? 1 : 2;
                case PluralGroup.Czech:
                    if (count == 1) return 0;
                    return count >= 2 && count <= 4 ? 1 : 2;
                case PluralGroup.Polish:
                    if (count == 1) return 0;
                    return 2 <= count && end <= 4 && (lastTwo < 10 || lastTwo >= 20) ? 1 : 2;
                case PluralGroup.Icelandic:
                    return end != 1 || lastTwo == 11 ? 1 : 0;
                case PluralGroup.Slovenian:
                    switch (lastTwo)
                    {
                        case 1:
                            return 0;
                        case 2:
                            return 1;
                        case 3:
                        case 4:
                            return 2;
                        default:
                            return 3;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private enum PluralGroup
        {
            Arabic,
            BosnianSerbian,
            Chinese,
            Croatian,
            French,
            German,
            Russian,
            Lithuanian,
            Czech,
            Polish,
            Icelandic,
            Slovenian
        }
    }
}