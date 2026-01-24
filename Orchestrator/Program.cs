using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Infra.Mongo;
using Orchestrator.Infra.Repositories;
using Orchestrator.Infra.Settings;
using Orchestrator.Infra.SignalR;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Interfaces;
using Orchestrator.UseCases.Security.Authorization;
using Orchestrator.UseCases.Security;


var builder = WebApplication.CreateBuilder(args);

BsonSerializer.RegisterSerializer(new GuidSerializer(MongoDB.Bson.BsonType.String));
BsonSerializer.RegisterSerializer(new DateTimeSerializer(MongoDB.Bson.BsonType.String));
BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(MongoDB.Bson.BsonType.String));

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt settings are missing.");

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = true;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateLifetime = true,
        ValidateAudience = true,
        ValidateIssuer = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Role:Player", policy =>
        policy.Requirements.Add(new MinimumRoleRequirement(RoleLevel.Player)));
    options.AddPolicy("Role:MainPlayer", policy =>
        policy.Requirements.Add(new MinimumRoleRequirement(RoleLevel.MainPlayer)));
    options.AddPolicy("Role:TeamLeader", policy =>
        policy.Requirements.Add(new MinimumRoleRequirement(RoleLevel.TeamLeader)));
    options.AddPolicy("Role:Admin", policy =>
        policy.Requirements.Add(new MinimumRoleRequirement(RoleLevel.Admin)));
    options.AddPolicy("Role:SuperAdmin", policy =>
        policy.Requirements.Add(new MinimumRoleRequirement(RoleLevel.SuperAdmin)));
});

builder.Services.AddControllers().AddJsonOptions(x =>
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactDevelopment",
        builder =>
        {
            builder.WithOrigins("http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

builder.Services.AddSignalR();

builder.Services.Configure<HibrygameDatabaseSettings>(
    builder.Configuration.GetSection("HibrygameDatabase"));

builder.Services.AddScoped<CreateUserUseCase>();
builder.Services.AddScoped<GetUserUseCase>();
builder.Services.AddScoped<LoginAsyncUseCase>();
builder.Services.AddScoped<UpdateUserUseCase>();
builder.Services.AddScoped<DeleteUserUseCase>();
builder.Services.AddScoped<ChangePasswordUseCase>();
builder.Services.AddScoped<RefreshTokenUseCase>();
builder.Services.AddScoped<ISecureHashingService, SecureHashingService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
builder.Services.AddScoped<IValidationService, ValidationService>();
builder.Services.AddScoped<IUserRepositoryNoSql, UserRepositoryNoNoSql>();
builder.Services.AddScoped<IValidationRepositoryNoSql, ValidationRepositoryNoSql>();
builder.Services.AddScoped<IRefreshTokenRepositoryNoSql, RefreshTokenRepositoryNoSql>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactDevelopment");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action}/{id?}");
app.MapHub<ChessHub>("/chesshub");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
