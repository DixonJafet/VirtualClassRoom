using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlX.XDevAPI.Common;
using Org.BouncyCastle.Pqc.Crypto.Utilities;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
namespace ProfessorAPI.Connexions
{

    public class MyJWT : PageModel
    {
        private readonly string tokenKey;
        private readonly IConfiguration config;

        public MyJWT(IConfiguration config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            tokenKey = config.GetSection("JWTkey").GetValue<string>("key") ?? throw new InvalidOperationException("JWT key is missing in configuration.");
        }

        public string userJWTSession(string user, string ID)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var bytekey = Encoding.UTF8.GetBytes(tokenKey);
            var tokenDes = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                    {
                        new Claim (ClaimTypes.Name,user),
                        new Claim (ClaimTypes.NameIdentifier,ID)
                    }
                ),
                Expires = DateTime.UtcNow.AddDays(5),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(bytekey),
                                                                SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDes);
            string str_token = tokenHandler.WriteToken(token);
            return str_token;
        }
    }
}
