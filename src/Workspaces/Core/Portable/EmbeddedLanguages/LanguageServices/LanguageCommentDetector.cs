﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.CodeAnalysis.EmbeddedLanguages.LanguageServices
{
    /// <summary>
    /// Helps match patterns of the form: language=name,option1,option2,option3
    /// 
    /// All matching is case insensitive, with spaces allowed between the punctuation. Option values will be or'ed
    /// together to produce final options value.  If an unknown option is encountered, processing will stop with
    /// whatever value has accumulated so far.
    /// 
    /// Option names are the values from the TOptions enum.
    /// </summary>
    internal struct LanguageCommentDetector<TOptions> where TOptions : struct, Enum
    {
        private static readonly Dictionary<string, TOptions> s_nameToOption =
            typeof(TOptions).GetTypeInfo().DeclaredFields
                .Where(f => f.FieldType == typeof(TOptions))
                .ToDictionary(f => f.Name, f => (TOptions)f.GetValue(null)!, StringComparer.OrdinalIgnoreCase);

        private readonly Regex _regex;

        public LanguageCommentDetector(params string[] languageNames)
        {
            var namePortion = string.Join("|", languageNames.Select(n => $"({Regex.Escape(n)})"));
            _regex = new Regex($@"^((//)|(')|(/\*))\s*lang(uage)?\s*=\s*({namePortion})\b((\s*,\s*)(?<option>[a-zA-Z]+))*",
                RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        public bool TryMatch(string text, out TOptions options)
        {
            var match = _regex.Match(text);
            options = default;
            if (!match.Success)
                return false;

            var optionGroup = match.Groups["option"];
            foreach (Capture? capture in optionGroup.Captures)
            {
                if (!s_nameToOption.TryGetValue(capture!.Value, out var specificOption))
                {
                    // hit something we don't understand.  bail out.  that will help ensure
                    // users don't have weird behavior just because they misspelled something.
                    // instead, they will know they need to fix it up.
                    return false;
                }

                options = CombineOptions(options, specificOption);
            }

            return true;
        }

        private static TOptions CombineOptions(TOptions options, TOptions specificOption)
        {
            var int1 = (int)(object)options;
            var int2 = (int)(object)specificOption;
            return (TOptions)(object)(int1 | int2);
        }
    }
}
