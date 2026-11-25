using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Application.Repositories;
using Fundoo.Notes.Application.Services;
using Fundoo.Notes.Infrastructure.Data;
using Fundoo.Notes.Infrastructure.Repositories;
using Fundoo.Notes.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.Notes.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddNotesInfrastructure(this IServiceCollection services,
            IConfiguration configuration)
        {
            // Database connection factory
            services.AddSingleton<NotesDbConnectionFactory>();
            services.AddDbContext<NotesDbContext>(options => options.UseNpgsql(configuration.GetConnectionString("NotesDb")));

            // Note Repository
            services.AddScoped<INotesRepository, NotesRepository>();
            services.AddScoped<INoteCollaboratorsRepository, NoteCollaboratorsRepository>();
            services.AddScoped<INoteCollaboratorsService, NoteCollaboratorsService>();
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, MemoryCacheService>();

            // Application service
            services.AddScoped<INotesService, NotesService>();
            services.AddSingleton<INoteEventPublisher, KafkaNoteEventPublisher>();

            // Label Repository
            services.AddScoped<ILabelsRepository, LabelsRepository>();

            // Label Service
            services.AddScoped<ILabelsService, LabelsService>();

            return services;
        }
    }
}
