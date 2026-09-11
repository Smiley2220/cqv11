using System;
using System.Linq;

namespace CampusQ.MVP.Models
{
    public class QueueEntry
    {
        public QueueEntry()
        {
            Purpose = string.Empty;
            Service = string.Empty;
            TimeAdded = DateTime.Now;
        }

        public int TicketNumber { get; set; }

        public int ServiceTicketNumber { get; set; }

        public string Purpose { get; set; }

        public string Service { get; set; }

        public DateTime TimeAdded { get; set; }

        public bool IsPriority { get; set; }

        public string TicketLabel
        {
            get
            {
                var seq = ServiceTicketNumber > 0 ? ServiceTicketNumber : TicketNumber;
                var prefix = BuildPrefix(Service, Purpose);
                return $"{prefix}-{seq:D3}";
            }
        }

        private static string BuildPrefix(string service, string purpose)
        {
            var s = GetFirstAlpha(service);
            var p = GetFirstAlpha(purpose);

            if (s != '\0' && p != '\0')
                return $"{char.ToUpperInvariant(s)}{char.ToUpperInvariant(p)}";

            var svcLetters = string.Concat((service ?? string.Empty).Where(char.IsLetter)).ToUpperInvariant();
            if (svcLetters.Length >= 2) return svcLetters.Substring(0, 2);

            var purLetters = string.Concat((purpose ?? string.Empty).Where(char.IsLetter)).ToUpperInvariant();
            if (purLetters.Length >= 2) return purLetters.Substring(0, 2);

            // last resort
            return "XX";
        }

        private static char GetFirstAlpha(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return '\0';
            foreach (var ch in text)
            {
                if (char.IsLetter(ch)) return ch;
            }
            return '\0';
        }
    }
}
