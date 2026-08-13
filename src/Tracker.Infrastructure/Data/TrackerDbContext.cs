using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tracker.Domain.Entities;
using Tracker.Infrastructure.Identity;
using Tracker.Infrastructure.Security;

namespace Tracker.Infrastructure.Data;

public class TrackerDbContext : IdentityDbContext<UsuarioADM, IdentityRole<Guid>, Guid>
{
    private readonly AesStringEncryptor _cpfEncryptor;

    public TrackerDbContext(DbContextOptions<TrackerDbContext> options, AesStringEncryptor cpfEncryptor)
        : base(options)
    {
        _cpfEncryptor = cpfEncryptor;
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Dispositivo> Dispositivos => Set<Dispositivo>();
    public DbSet<Local> Locais => Set<Local>();
    public DbSet<HistoricoMovimentacao> HistoricoMovimentacoes => Set<HistoricoMovimentacao>();
    public DbSet<HistoricoLocalizacao> HistoricoLocalizacoes => Set<HistoricoLocalizacao>();
    public DbSet<TermoDeUso> TermosDeUso => Set<TermoDeUso>();
    public DbSet<RegraDeGeofence> RegrasDeGeofence => Set<RegraDeGeofence>();
    public DbSet<ComandoRemoto> ComandosRemotos => Set<ComandoRemoto>();
    public DbSet<HistoricoReparo> HistoricoReparos => Set<HistoricoReparo>();
    public DbSet<ComentarioDispositivo> ComentariosDispositivo => Set<ComentarioDispositivo>();
    public DbSet<AlertaDispositivo> AlertasDispositivo => Set<AlertaDispositivo>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var cpfConverter = new EncryptedStringConverter(_cpfEncryptor);

        builder.Entity<Usuario>(entity =>
        {
            entity.Property(u => u.Cpf).HasConversion(cpfConverter);
            entity.HasIndex(u => u.Email).IsUnique();
        });

        builder.Entity<UsuarioADM>(entity =>
        {
            entity.Property(a => a.Cpf).HasConversion(cpfConverter);
        });

        // TPH: uma única tabela Dispositivos com discriminador TipoDispositivo.
        builder.Entity<Dispositivo>(entity =>
        {
            entity.HasDiscriminator(d => d.Tipo)
                .HasValue<Notebook>(Domain.Enums.TipoDispositivo.Notebook)
                .HasValue<Mobile>(Domain.Enums.TipoDispositivo.Mobile)
                .HasValue<Periferico>(Domain.Enums.TipoDispositivo.Periferico);

            entity.HasIndex(d => d.NumeroSerie).IsUnique();
            // Filtrado: periféricos não têm patrimônio, e vários nulos quebrariam o índice único.
            entity.HasIndex(d => d.AssetTag).IsUnique().HasFilter("[AssetTag] IS NOT NULL");

            entity.HasOne(d => d.UsuarioAtual)
                .WithMany(u => u.Dispositivos)
                .HasForeignKey(d => d.UsuarioAtualId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // IMEI mora só em Mobile (Notebook não tem). Nulo porque tablet só-Wi-Fi não tem modem.
        builder.Entity<Mobile>().Property(m => m.Imei).HasColumnName("Imei").IsRequired(false);

        builder.Entity<HistoricoMovimentacao>(entity =>
        {
            entity.HasOne(h => h.Dispositivo)
                .WithMany(d => d.HistoricoMovimentacoes)
                .HasForeignKey(h => h.DispositivoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.UsuarioOrigem)
                .WithMany()
                .HasForeignKey(h => h.UsuarioOrigemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.UsuarioDestino)
                .WithMany()
                .HasForeignKey(h => h.UsuarioDestinoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.LocalOrigem)
                .WithMany()
                .HasForeignKey(h => h.LocalOrigemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.LocalDestino)
                .WithMany()
                .HasForeignKey(h => h.LocalDestinoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<HistoricoLocalizacao>(entity =>
        {
            entity.HasOne(h => h.Dispositivo)
                .WithMany(d => d.HistoricoLocalizacoes)
                .HasForeignKey(h => h.DispositivoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(h => new { h.DispositivoId, h.CapturadoEm });
        });

        builder.Entity<TermoDeUso>(entity =>
        {
            entity.HasOne(t => t.Dispositivo)
                .WithMany()
                .HasForeignKey(t => t.DispositivoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Usuario)
                .WithMany()
                .HasForeignKey(t => t.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RegraDeGeofence>(entity =>
        {
            entity.HasOne(r => r.Dispositivo)
                .WithMany()
                .HasForeignKey(r => r.DispositivoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Local)
                .WithMany()
                .HasForeignKey(r => r.LocalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ComandoRemoto>(entity =>
        {
            entity.HasOne(c => c.Dispositivo)
                .WithMany()
                .HasForeignKey(c => c.DispositivoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<HistoricoReparo>(entity =>
        {
            entity.HasOne(r => r.Dispositivo)
                .WithMany(d => d.HistoricoReparos)
                .HasForeignKey(r => r.DispositivoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(r => r.CustoEstimado).HasColumnType("decimal(18,2)");
        });

        builder.Entity<ComentarioDispositivo>(entity =>
        {
            entity.HasOne(c => c.Dispositivo)
                .WithMany(d => d.Comentarios)
                .HasForeignKey(c => c.DispositivoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AlertaDispositivo>(entity =>
        {
            entity.HasOne(a => a.Dispositivo)
                .WithMany(d => d.Alertas)
                .HasForeignKey(a => a.DispositivoId)
                .OnDelete(DeleteBehavior.Cascade);

            // A tela do dispositivo lista primeiro os alertas não reconhecidos, mais recentes antes.
            entity.HasIndex(a => new { a.DispositivoId, a.Reconhecido, a.CriadoEm });
        });
    }
}
