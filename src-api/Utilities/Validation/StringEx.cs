namespace Kitsune.SDK.Utilities
{
    public static class StringEx
    {
        /// <summary>
        /// Core identifier validation logic for names, SQL identifiers, placeholders, etc.
        /// Verifies that an identifier only contains allowed characters.
        /// </summary>
        /// <param name="identifier">The identifier to validate</param>
        /// <param name="allowedSpecialChars">Additional special characters to allow besides alphanumeric</param>
        /// <param name="paramName">Parameter name for exception (optional)</param>
        /// <param name="errorMessage">Custom error message (optional)</param>
        /// <returns>The validated identifier</returns>
        /// <exception cref="ArgumentException">Thrown when the identifier is invalid</exception>
        private static string ValidateIdentifier(this string identifier, char[] allowedSpecialChars, string? paramName = null, string? errorMessage = null)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(errorMessage ?? "Identifier cannot be empty", paramName);
            }

            // Create a minimal lookup table for faster character checking
            // Since the special chars we use (_, -, ., {, }) are all below ASCII 128
            // We can use a direct lookup approach rather than linear search
            var isAllowedSpecialChar = new bool[128];
            foreach (char c in allowedSpecialChars)
            {
                isAllowedSpecialChar[c] = true;
            }

            // Check for invalid characters
            for (int i = 0; i < identifier.Length; i++)
            {
                char c = identifier[i];
                if (char.IsLetterOrDigit(c))
                    continue;

                // Fast path for ASCII special chars
                if (c < 128 && isAllowedSpecialChar[c])
                    continue;

                // Character is invalid
                string allowedCharsDesc = string.Join(", ", allowedSpecialChars.Select(ch => $"'{ch}'"));
                throw new ArgumentException(errorMessage ?? $"Identifier can only contain alphanumeric characters and {allowedCharsDesc} (invalid character: '{c}')", paramName);
            }

            return identifier;
        }

        /// <summary>
        /// Escape a string for TOML format
        /// </summary>
        public static string EscapeToml(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// Validates a name for configuration items, commands, settings, storage
        /// Throws ArgumentException if the name is invalid.
        /// </summary>
        /// <param name="name">The name to validate</param>
        /// <param name="paramName">The parameter name for the exception</param>
        /// <exception cref="ArgumentException">Thrown when the name is invalid</exception>
        public static void ValidateName(this string name, string paramName)
            => ValidateIdentifier(name, ['_', '-'], paramName, "Name can only contain alphanumeric characters, underscores, and hyphens");

        /// <summary>
        /// Validates a placeholder name - allows curly braces in addition to alphanumeric characters, underscores, and hyphens.
        /// Throws ArgumentException if the name is invalid.
        /// </summary>
        /// <param name="placeholder">The placeholder to validate</param>
        /// <param name="paramName">The parameter name for the exception</param>
        /// <exception cref="ArgumentException">Thrown when the placeholder is invalid</exception>
        public static void ValidatePlaceholder(this string placeholder, string paramName)
            => ValidateIdentifier(placeholder, ['_', '-', '{', '}'], paramName, "Placeholder can only contain alphanumeric characters, underscores, hyphens, and curly braces");

        /// <summary>
        /// Validates a SQL identifier (table or column name) to prevent SQL injection.
        /// Returns the identifier if valid, throws ArgumentException if invalid.
        /// </summary>
        /// <param name="identifier">The SQL identifier to validate</param>
        /// <returns>The validated identifier</returns>
        /// <exception cref="ArgumentException">Thrown when the identifier contains invalid characters</exception>
        public static string ValidateSqlIdentifier(this string identifier)
            => ValidateIdentifier(identifier, ['_', '.', '-'], errorMessage: "SQL identifier can only contain alphanumeric characters, underscores, dots, and hyphens");
    }
}