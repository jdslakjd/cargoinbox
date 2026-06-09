using System.Text;
using CargoInbox.Application.Services;
using CargoInbox.Api.Hubs;
using CargoInbox.Core.Configurations;
using CargoInbox.Core.Interfaces;
using CargoInbox.Infrastructure.Data;
using CargoInbox.Localization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pgvector.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=cargoinbox_db;Username=postgres;Password=your_password";
builder.Services.AddDbContext<CargoInboxContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector()));

var jwtKey = builder.Configuration["Jwt:Key"] ?? "CargoInbox_Super_Secret_Security_Key_2026_Top_Secret";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "CargoInboxServer",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "CargoInboxClient",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddCors(options => options.AddPolicy("AngularCors", policy =>
    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CargoInbox 核心协同收件箱后端中枢",
        Version = "v1",
        Description = "高并发、双向向量化多渠道协同看板系统 API 交互式联调大盘",
        Contact = new OpenApiContact { Name = "CargoInbox 研发组" }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "请输入你的前端标准 JWT 安全访问令牌。格式举例：Bearer {Your_Token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", null, "SecurityScheme"),
            new List<string>()
        }
    });
});

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ =>
    StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString!));

builder.Services.AddScoped<RedisCollaborationService>();
builder.Services.AddScoped<CustomerCaptureService>();
builder.Services.AddScoped<ContactCaptureService>();
builder.Services.AddHttpClient();
var googleOAuthSection = builder.Configuration.GetSection("GoogleOAuth");
builder.Services.Configure<CargoInbox.Api.Controllers.GoogleOAuthOptions>(googleOAuthSection);
var microsoftOAuthSection = builder.Configuration.GetSection("MicrosoftOAuth");
builder.Services.Configure<CargoInbox.Api.Controllers.MicrosoftOAuthOptions>(microsoftOAuthSection);
var llmSection = builder.Configuration.GetSection("LlmSettings");
builder.Services.Configure<CargoInbox.Core.Configurations.AiSettings>(llmSection);

builder.Services.AddHttpClient<AiTranslationService>();
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<IMailSyncService, MailSyncService>();
builder.Services.AddScoped<RulesEngineProcessor>();
builder.Services.AddScoped<MailSendService>();
builder.Services.AddScoped<SlaTrackerService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ExpressionRuleEvaluator>();
builder.Services.AddScoped<ApprovalWorkflowService>();
builder.Services.AddScoped<AttachmentStorageService>();
builder.Services.AddScoped<SequenceEngineService>();
builder.Services.AddScoped<AiTranslationService>();
builder.Services.AddScoped<TemplateVariableEngine>();
builder.Services.AddScoped<ChannelOutboundService>();
builder.Services.AddScoped<InboxPermissionService>();
builder.Services.AddScoped<InboundConversationService>();
builder.Services.AddScoped<CrmActivityService>();
builder.Services.AddScoped<CrmTimelineService>();
builder.Services.AddScoped<PipelineService>();
builder.Services.AddScoped<RoundRobinAssignmentService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<LiveChatService>();
builder.Services.AddScoped<CrmCustomFieldService>();
builder.Services.AddSingleton<CrmSegmentEvaluator>();
builder.Services.AddHostedService<ScheduledMessageWorker>();
builder.Services.AddScoped<CalendarCollisionService>();
builder.Services.AddSingleton<MailResiliencePolicy>();
builder.Services.AddSingleton<GmailApiService>();
builder.Services.AddSingleton<OutlookApiService>();
builder.Services.AddSingleton<EmailThreadingService>();
builder.Services.AddHostedService<SlaMonitorBackgroundService>();
builder.Services.AddHostedService<EmailSyncEngineWorker>();
builder.Services.AddHostedService<SnoozeAutoWakeWorker>();
builder.Services.AddHostedService<SequenceStepWorker>();

builder.Services.AddControllers();

builder.Services.AddLocalization();
builder.Services.AddScoped<I18nService>();
builder.Services.AddScoped<TimezoneService>();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "zh-CN", "en-US", "en" };
    options.SetDefaultCulture(supportedCultures[0]);
    options.AddSupportedCultures(supportedCultures);
    options.AddSupportedUICultures(supportedCultures);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CargoInbox v1");
    c.RoutePrefix = "swagger";
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
});

app.UseRequestLocalization();
app.UseMiddleware<CargoInbox.Api.Middleware.RateLimitMiddleware>();
app.UseStaticFiles();
app.UseCors("AngularCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<CargoInbox.Api.Hubs.CollaborationHub>("/hub/collaboration");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
    await db.Database.MigrateAsync();
}

app.Run();
