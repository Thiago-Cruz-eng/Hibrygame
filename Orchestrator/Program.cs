using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Orchestrator.Infra.BaseRepository;
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

// HS256 exige chave de no minimo 256 bits. A chave que vinha no appsettings.json tinha
// 240, e o efeito era desagradavel de diagnosticar: a aplicacao subia normalmente e
// TODO login falhava, porque CreateAccessToken estourava IDX10720 la dentro e o
// use case devolvia um "Login failed" generico com 401 — indistinguivel de senha errada.
// Melhor falhar aqui, na subida, dizendo o motivo.
const int minimumKeyBytes = 32;
var signingKey = jwtSettings.Key ?? string.Empty;
var keyBytes = Encoding.UTF8.GetByteCount(signingKey);
if (keyBytes < minimumKeyBytes)
{
    throw new InvalidOperationException(
        $"Jwt:Key tem {keyBytes} bytes; HS256 exige pelo menos {minimumKeyBytes} " +
        "(256 bits). Com uma chave menor nenhum token pode ser assinado e todo login " +
        "falha. Ajuste Jwt:Key na configuracao.");
}

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ClockSkew = TimeSpan.Zero
    };
    x.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var accessToken = ctx.Request.Query["access_token"];
            var path = ctx.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chesshub"))
            {
                ctx.Token = accessToken;
            }
            return Task.CompletedTask;
        }
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

builder.Services.AddSingleton<IMongoDbContextFactory, MongoDbContextFactory>();
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var connection = builder.Configuration.GetSection("Mongo:ConnectionString").Value
                     ?? "mongodb://localhost:27017";
    return new MongoClient(connection);
});
builder.Services.AddSingleton<IMongoDbContext>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var databaseName = builder.Configuration.GetSection("Mongo:Database").Value ?? "Hibrygame";
    return new MongoDbContext(client.GetDatabase(databaseName));
});
builder.Services.AddScoped<IGenericRepository, GenericRepository>();
builder.Services.AddScoped<IUserRepositoryNoSql, UserRepositoryNoSql>();
builder.Services.AddScoped<IRefreshTokenRepositoryNoSql, RefreshTokenRepositoryNoSql>();
builder.Services.AddScoped<IValidationRepositoryNoSql, ValidationRepositoryNoSql>();

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowReactDevelopment");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChessHub>("/chesshub");

app.Run();
