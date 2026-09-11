using System;
using System.Linq;

namespace CampusQ.MVP.Models
{
    public class QueuePersistDto
    {
        public int TicketNumber { get; set; }
        public int ServiceTicketNumber { get; set; }

        public string Purpose { get; set; } = "";
        public string Service { get; set; } = "";
        public DateTime TimeAdded { get; set; }
        public DateTime? ServedAt { get; set; }

        public string TicketLabel
        {
            get
            {
                var number = ServiceTicketNumber > 0 ? ServiceTicketNumber : TicketNumber;
                var prefix = BuildPrefix(Service, Purpose);
                return $"{prefix}-{number:D3}";
            }
        }

        private static string BuildPrefix(string service, string purpose)
        {
            var s = !string.IsNullOrWhiteSpace(service) ? char.ToUpperInvariant(service.Trim()[0]) : 'O';
            var pChar = GetFirstAlpha(purpose);
            if (pChar == '\0') return s.ToString();
            return $"{s}{pChar}";
        }

        private static char GetFirstAlpha(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return '\0';
            foreach (var ch in text)
            {
                if (char.IsLetter(ch)) return char.ToUpperInvariant(ch);
            }
            return '\0';
        }
    }
}
