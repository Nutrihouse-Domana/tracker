package br.com.nutrihouse.tracker.agent.enrollment

import android.Manifest
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.PowerManager
import android.provider.Settings
import android.view.View
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.widget.doOnTextChanged
import androidx.lifecycle.lifecycleScope
import br.com.nutrihouse.tracker.agent.BuildConfig
import br.com.nutrihouse.tracker.agent.R
import br.com.nutrihouse.tracker.agent.dados.Armazenamento
import br.com.nutrihouse.tracker.agent.dados.BackendClient
import br.com.nutrihouse.tracker.agent.dados.CodigoInvalidoException
import br.com.nutrihouse.tracker.agent.databinding.ActivityEnrollmentBinding
import br.com.nutrihouse.tracker.agent.dispositivo.InfoAparelho
import br.com.nutrihouse.tracker.agent.dispositivo.NivelProtecao
import br.com.nutrihouse.tracker.agent.dispositivo.Politica
import br.com.nutrihouse.tracker.agent.localizacao.Geolocalizacao
import br.com.nutrihouse.tracker.agent.servico.AgenteService
import kotlinx.coroutines.launch

/**
 * Tela de instalacao usada pelo tecnico de TI: registra o aparelho no inventario e conduz a
 * concessao das quatro permissoes de que o agente depende.
 *
 * Depois do registro ela deixa de ser um passo obrigatorio — vira o painel de diagnostico do
 * aparelho, mostrando o que ainda falta.
 */
class EnrollmentActivity : AppCompatActivity() {

    private lateinit var binding: ActivityEnrollmentBinding

    private val armazenamento by lazy { Armazenamento(this) }
    private val politica by lazy { Politica(this) }
    private val backend by lazy { BackendClient(this, armazenamento) }
    private val info by lazy { InfoAparelho(this) }

    private val pedirLocalizacao = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions(),
    ) { atualizarTela() }

    private val voltarDeTelaDoSistema = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult(),
    ) { atualizarTela() }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityEnrollmentBinding.inflate(layoutInflater)
        setContentView(binding.root)

        // O código de instalação é sempre digitado aqui; some da tela depois do registro.
        binding.campoChave.doOnTextChanged { _, _, _, _ -> binding.blocoChave.error = null }
        binding.campoNumeroSerie.doOnTextChanged { _, _, _, _ -> binding.blocoNumeroSerie.error = null }
        binding.campoChave.setOnEditorActionListener { _, _, _ -> registrar(); true }

        binding.botaoRegistrar.setOnClickListener { registrar() }
        binding.botaoAdmin.setOnClickListener { pedirAdmin() }
        binding.botaoLocalizacao.setOnClickListener { pedirPermissaoLocalizacao() }
        binding.botaoOverlay.setOnClickListener { pedirSobreposicao() }
        binding.botaoBateria.setOnClickListener { pedirIsencaoDeBateria() }
        binding.botaoInstalarApps.setOnClickListener { pedirPermissaoDeInstalar() }
    }

    override fun onResume() {
        super.onResume()
        atualizarTela()
    }

    private fun registrar() {
        if (armazenamento.registrado) return

        // trim(): o teclado do Android costuma acrescentar um espaço ao aceitar a palavra.
        val codigo = binding.campoChave.text?.toString()?.trim().orEmpty()
        if (codigo.isBlank()) {
            binding.blocoChave.error = getString(R.string.enrollment_chave_vazia)
            return
        }

        // Identificadores digitados pelo tecnico: ligam o registro ao ativo real quando o Android
        // nao entrega o serial/IMEI ao app (qualquer aparelho sem Device Owner).
        val numeroSerie = binding.campoNumeroSerie.text?.toString()?.trim()

        // Serie obrigatoria quando o Android nao a entrega: sem ela o aparelho entraria com o
        // identificador provisorio, que muda se o APK for reinstalado com outra assinatura — foi
        // o que ja duplicou registros no inventario. Com serie de fabrica disponivel o campo e
        // dispensavel, porque o proprio agente a informa.
        if (numeroSerie.isNullOrBlank() && info.numeroSerieReal == null) {
            binding.blocoNumeroSerie.error = getString(R.string.enrollment_numero_serie_vazio)
            return
        }

        binding.botaoRegistrar.isEnabled = false

        lifecycleScope.launch {
            // Só a série identifica o aparelho: patrimônio e IMEI ficam a cargo do painel, onde a
            // TI já mantém o cadastro do ativo.
            val resultado = runCatching { backend.registrar(codigo, numeroSerieInformado = numeroSerie) }

            binding.botaoRegistrar.isEnabled = true
            resultado
                .onSuccess {
                    // Limpa o campo: o código não fica na tela nem é guardado no aparelho.
                    binding.campoChave.text = null
                    aviso(getString(R.string.enrollment_registrado))
                    AgenteService.iniciar(this@EnrollmentActivity)
                    atualizarTela()
                }
                .onFailure { erro ->
                    // 401 do backend = código errado; o resto é falha de rede/servidor.
                    binding.blocoChave.error = if (erro is CodigoInvalidoException) {
                        getString(R.string.enrollment_chave_invalida)
                    } else {
                        erro.message ?: getString(R.string.enrollment_falha_generica)
                    }
                }
        }
    }

    private fun pedirAdmin() {
        if (politica.ehDeviceOwner) {
            aviso("Este aparelho já é gerenciado como Device Owner.")
            return
        }
        voltarDeTelaDoSistema.launch(politica.intentParaAtivarAdmin())
    }

    private fun pedirPermissaoLocalizacao() {
        val permissoes = mutableListOf(
            Manifest.permission.ACCESS_FINE_LOCATION,
            Manifest.permission.ACCESS_COARSE_LOCATION,
        )

        // A localizacao em segundo plano so pode ser pedida depois da permissao comum: pedir as
        // duas juntas faz o Android negar as duas silenciosamente.
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q && jaTemLocalizacaoComum()) {
            permissoes += Manifest.permission.ACCESS_BACKGROUND_LOCATION
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            permissoes += Manifest.permission.POST_NOTIFICATIONS
        }

        // Ate o Android 9 esta permissao libera o numero de serie e o IMEI de fabrica. Do 10 em
        // diante o sistema recusa a leitura a app comum, entao pedi-la seria incomodo sem ganho.
        if (Build.VERSION.SDK_INT <= Build.VERSION_CODES.P) {
            permissoes += Manifest.permission.READ_PHONE_STATE
        }

        pedirLocalizacao.launch(permissoes.toTypedArray())
    }

    private fun jaTemLocalizacaoComum(): Boolean =
        checkSelfPermission(Manifest.permission.ACCESS_FINE_LOCATION) ==
            android.content.pm.PackageManager.PERMISSION_GRANTED

    private fun pedirSobreposicao() {
        if (politica.podeSobreporJanelas) {
            aviso("Permissão de sobreposição já concedida.")
            return
        }

        voltarDeTelaDoSistema.launch(
            Intent(
                Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
                Uri.parse("package:$packageName"),
            ),
        )
    }

    private fun pedirIsencaoDeBateria() {
        if (semRestricaoDeBateria()) {
            aviso("O agente já está isento da otimização de bateria.")
            return
        }

        // Tela geral da lista: o dialogo direto (ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS) e
        // motivo de remocao da Play Store, e este APK e distribuido internamente de todo jeito.
        voltarDeTelaDoSistema.launch(Intent(Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS))
    }

    private fun semRestricaoDeBateria(): Boolean =
        getSystemService(PowerManager::class.java).isIgnoringBatteryOptimizations(packageName)

    /**
     * "Instalar apps desconhecidos": e o que permite a atualizacao OTA sem loja quando o
     * aparelho nao e Device Owner (que instala sem pedir nada).
     */
    private fun podeInstalarAtualizacoes(): Boolean = when {
        politica.ehDeviceOwner -> true
        Build.VERSION.SDK_INT >= Build.VERSION_CODES.O -> packageManager.canRequestPackageInstalls()
        else -> true // Antes do Android 8 vale a configuracao global de fontes desconhecidas.
    }

    private fun pedirPermissaoDeInstalar() {
        if (podeInstalarAtualizacoes()) {
            aviso("O agente já pode instalar as próprias atualizações.")
            return
        }

        voltarDeTelaDoSistema.launch(
            Intent(
                Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                Uri.parse("package:$packageName"),
            ),
        )
    }

    /** Redesenha o diagnostico: o que ja esta pronto e o que ainda falta. */
    private fun atualizarTela() {
        val linhas = mutableListOf<String>()

        linhas += if (armazenamento.registrado) {
            "✔ Registrado no inventário (${info.tipo.name.lowercase()})"
        } else {
            "✖ Ainda não registrado no inventário"
        }

        linhas += when (politica.nivelProtecao) {
            NivelProtecao.DeviceOwner -> "✔ Proteção máxima (Device Owner)"
            NivelProtecao.DeviceAdmin -> "✔ Administrador ativo (o usuário ainda pode desativar)"
            NivelProtecao.Nenhuma -> "✖ Sem permissão de administrador: bloqueio remoto indisponível"
        }

        linhas += if (Geolocalizacao(this).temPermissao) {
            "✔ Localização permitida"
        } else {
            "✖ Localização não permitida"
        }

        linhas += if (politica.podeSobreporJanelas) {
            "✔ Sobreposição permitida"
        } else {
            "✖ Sobreposição não permitida: a tela de bloqueio pode não aparecer"
        }

        linhas += if (semRestricaoDeBateria()) {
            "✔ Bateria sem restrição"
        } else {
            "✖ Bateria restrita: o aparelho pode aparecer offline"
        }

        linhas += if (podeInstalarAtualizacoes()) {
            "✔ Atualizações automáticas permitidas"
        } else {
            "✖ Sem permissão de instalar: atualizações do agente não se aplicam"
        }

        // Distingue o serial de fabrica do identificador provisorio: o provisorio muda quando o
        // APK e reinstalado com outra assinatura, e e por isso que a serie precisa ser digitada —
        // sem ela o aparelho voltaria ao inventario como um ativo novo.
        val serieReal = info.numeroSerieReal
        linhas += if (serieReal != null) {
            "✔ Série de fábrica: $serieReal"
        } else {
            "Série provisória: ${info.numeroSerie}\n" +
                "  (o Android não libera a real sem Device Owner — informe a série no registro)"
        }

        linhas += "Versão do agente: ${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE})"

        binding.textoStatus.text = linhas.joinToString("\n")

        // Depois de registrado os campos e o botão saem da tela: ela passa a servir só
        // como diagnóstico do aparelho.
        val precisaRegistrar = !armazenamento.registrado
        binding.blocoChave.visibility = if (precisaRegistrar) View.VISIBLE else View.GONE
        binding.blocoNumeroSerie.visibility = if (precisaRegistrar) View.VISIBLE else View.GONE
        binding.botaoRegistrar.visibility = if (precisaRegistrar) View.VISIBLE else View.GONE
        binding.botaoRegistrar.isEnabled = precisaRegistrar
    }

    private fun aviso(mensagem: String) {
        Toast.makeText(this, mensagem, Toast.LENGTH_LONG).show()
    }
}
