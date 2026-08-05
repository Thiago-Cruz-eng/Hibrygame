using Orchestrator.Composition;
using Orchestrator.Infra.SignalR;

// -----------------------------------------------------------------------------------------------
// Composição da aplicação.
//
// Este arquivo é o índice: cada linha diz O QUE é registrado, e o COMO fica nas classes de
// Composition/, uma por assunto. Antes eram 170 linhas corridas aqui, e achar onde uma política de
// autorização era declarada exigia ler o arquivo inteiro.
//
// O registro continua sendo MANUAL, um serviço por vez — sem Scrutor e sem varredura de assembly.
// É decisão de arquitetura, não descuido: a composição inteira do sistema tem de ser legível.
//
// Ordem importa em dois pontos, e só neles:
//   1. os serializadores do Mongo, antes de qualquer operação com o banco;
//   2. a leitura das configurações de JWT, antes de configurar a autenticação que as usa.
// -----------------------------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// Como Guid e DateTime são gravados no MongoDB. Global e irreversível — ver a nota do método.
PersistenceComposition.RegisterBsonSerializers();

// Lê a seção "Jwt" e derruba a subida se a chave for curta demais para HS256. Devolve as
// configurações porque a autenticação precisa delas antes de a injeção de dependências existir.
var jwtSettings = builder.Services.AddJwtSettings(builder.Configuration);

builder.Services.AddJwtAuthentication(jwtSettings);
builder.Services.AddRolePolicies();
builder.Services.AddWebLayer();
builder.Services.AddMongoPersistence(builder.Configuration);
builder.Services.AddUseCases();

var app = builder.Build();

// -----------------------------------------------------------------------------------------------
// Pipeline de requisição.
//
// Aqui a ordem importa em TUDO: cada middleware envolve os seguintes, e trocar dois de lugar muda
// o comportamento. Em particular, UseAuthentication tem de vir antes de UseAuthorization —
// autorizar exige saber quem é o chamador, e é a autenticação que descobre isso.
// -----------------------------------------------------------------------------------------------

// Swagger só em desenvolvimento: em produção ele publicaria o mapa completo da API.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Redirecionamento para HTTPS só fora de desenvolvimento.
//
// Em producao e obrigatorio. Em desenvolvimento local ele so cria atrito: com o perfil
// "https" do launchSettings, as duas portas ficam configuradas, o middleware descobre o
// destino https e passa a responder 307 a todo http. O frontend segue o redirecionamento e
// morre no certificado de desenvolvimento — que em maquina corporativa gerenciada nem sempre
// pode ser marcado como confiavel, porque `dotnet dev-certs https --trust` exige permissao
// que o usuario nao tem.
//
// O sintoma e cruel: o login falha sem erro claro, e nada na tela aponta para certificado.
// Com o perfil "http" o middleware fica inerte (nao ha porta https para onde mandar) e tudo
// funciona — o que torna o comportamento dependente de qual perfil alguem escolheu no
// Rider, e isso nao e um bom contrato de ambiente local.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// CORS antes da autenticação: a requisição de verificação que o navegador manda antes da real
// (preflight, um OPTIONS) não carrega credencial nenhuma. Se a autenticação a examinasse primeiro,
// ela seria recusada e a requisição real nunca aconteceria.
app.UseCors(WebComposition.CorsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// O caminho "/chesshub" é contrato com o frontend, e aparece também no filtro que autoriza token
// por query string (ver JwtComposition). Mudar aqui exige mudar lá.
app.MapHub<ChessHub>("/chesshub");

app.Run();
