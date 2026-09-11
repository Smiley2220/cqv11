using System;
using System.Security.Cryptography;
using System.Text;

namespace CampusQ.Core.Infrastructure.Authentication
{
    /// <summary>
    /// Provides password hashing and verification services.
    /// Uses PBKDF2 (Password-Based Key Derivation Function 2) for secure password storage.
    /// </summary>
    public class PasswordHashService
    {
        private const int HashSize = 20; // 20 bytes for PBKDF2
        private const int SaltSize = 16; // 16 bytes
        private const int Iterations = 10000; // OWASP recommended minimum

        /// <summary>
        /// Hashes a password with a random salt using PBKDF2.
        /// Returns a string combining salt+hash for storage.
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password cannot be empty", nameof(password));
            }

            // Generate random salt
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] salt = new byte[SaltSize];
                rng.GetBytes(salt);

                // Derive hash
                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
                {
                    byte[] hash = pbkdf2.GetBytes(HashSize);

                    // Combine salt + hash
                    byte[] combined = new byte[SaltSize + HashSize];
                    Array.Copy(salt, 0, combined, 0, SaltSize);
                    Array.Copy(hash, 0, combined, SaltSize, HashSize);

                    // Return as base64 for storage
                    return Convert.ToBase64String(combined);
                }
            }
        }

        /// <summary>
        /// Verifies a password against a stored hash.
        /// </summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            try
            {
                // Decode stored hash
                byte[] combined = Convert.FromBase64String(storedHash);

                if (combined.Length != SaltSize + HashSize)
                {
                    return false;
                }

                // Extract salt
                byte[] salt = new byte[SaltSize];
                Array.Copy(combined, 0, salt, 0, SaltSize);

                // Extract stored hash
                byte[] storedHashBytes = new byte[HashSize];
                Array.Copy(combined, SaltSize, storedHashBytes, 0, HashSize);

                // Compute hash of provided password with same salt
                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
                {
                    byte[] computedHash = pbkdf2.GetBytes(HashSize);

                    // Compare hashes (constant-time comparison to prevent timing attacks)
                    return ConstantTimeEquals(storedHashBytes, computedHash);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Constant-time byte array comparison to prevent timing attacks.
        /// </summary>
        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }
    }

    /// <summary>
    /// Represents a user authentication context.
    /// </summary>
    public class AuthenticationContext
    {
        public string? Username { get; set; }
        public string? Role { get; set; }
        public bool IsAuthenticated { get; set; }
        public DateTime? AuthenticatedAt { get; set; }

        public override string ToString()
        {
            return IsAuthenticated
                ? $"User: {Username}, Role: {Role}"
                : "Not Authenticated";
        }
    }
}
