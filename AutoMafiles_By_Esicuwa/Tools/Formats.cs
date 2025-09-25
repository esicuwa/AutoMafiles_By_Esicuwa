

namespace AutoMafiles_By_Esicuwa.Tools
{
   
    public class Formats_Steam
    {
        public interface IAccount
        {
            string Login { get; set; }
            string Password { get; set; }

        }
        public class Standart : IAccount
        {
            public required string Login { get; set; } 
            public required string Password { get; set; }
            public required string Email { get; set; }
            public required string EmailPassword { get; set; }

        }
        public class Standart_IMAP_Password : IAccount
        {
            public required string Login { get; set; }
            public required string Password { get; set; }
            public required string Email { get; set; }
            public required string EmailPassword { get; set; }
            public required string ImapPassword { get; set; }


        }

        public class Outlook : IAccount
        {
            public required string Login { get; set; }
            public required string Password { get; set; }
            public required string Email { get; set; }
            public required string EmailPassword { get; set; }
            public required string RefreshToken { get; set; }
            public required string ClientId { get; set; }

        }
        public class Outlook_No_Password : IAccount
        {
            public required string Login { get; set; }
            public required string Password { get; set; }
            public required string Email { get; set; }
            public required string RefreshToken { get; set; }
            public required string ClientId { get; set; }

        }

    }
}
