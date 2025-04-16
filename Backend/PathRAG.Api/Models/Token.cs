namespace PathRAG.Api.Models
{
    public class TokenRequest
    {
        public string Code { get; set; }
        public string State { get; set; }
    }

    public class TokenResponse
    {
        public string Access_token { get; set; }
        public string Id_token { get; set; }
        public string Refresh_token { get; set; }
        public string Token_type { get; set; }
        public int Expires_in { get; set; }

        // Property accessors for camelCase naming convention
        public string AccessToken => Access_token;
        public string IdToken => Id_token;
        public string RefreshToken => Refresh_token;
        public string TokenType => Token_type;
        public int ExpiresIn => Expires_in;
    }
}
