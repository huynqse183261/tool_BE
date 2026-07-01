using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Repositories;
using Repositories.Entities;
using Services.Interface;
using Services.Models.Common;
using Services.Models.Request;
using Services.Models.Response;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Services.Implement
{
    public class AuthService : IAuthService
    {
        private readonly UserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public AuthService(
            UserRepository userRepository,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
        }

        public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request)
        {
            var existingUser = await _userRepository
                .GetByEmailAsync(request.Email);

            if (existingUser != null)
            {
                return new ApiResponse<AuthResponse>
                {
                    Success = false,
                    Message = "Email already exists"
                };
            }

            var user = new User
            {
                Email = request.Email,
                FullName = request.FullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            await _userRepository.CreateAsync(user);

            var response = new AuthResponse
            {
                Email = user.Email,
                FullName = user.FullName,
                AccessToken = GenerateJwtToken(user)
            };

            return new ApiResponse<AuthResponse>
            {
                Success = true,
                Message = "Register successful",
                Data = response
            };
        }

        public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request)
        {
            var user = await _userRepository
                .GetByEmailAsync(request.Email);

            if (user == null)
            {
                return new ApiResponse<AuthResponse>
                {
                    Success = false,
                    Message = "Invalid email or password"
                };
            }

            var validPassword = BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash
            );

            if (!validPassword)
            {
                return new ApiResponse<AuthResponse>
                {
                    Success = false,
                    Message = "Invalid email or password"
                };
            }

            var response = new AuthResponse
            {
                Email = user.Email,
                FullName = user.FullName,
                AccessToken = GenerateJwtToken(user)
            };

            return new ApiResponse<AuthResponse>
            {
                Success = true,
                Message = "Login successful",
                Data = response
            };
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email)
        };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _configuration["Jwt:Key"]
                )
            );

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}
