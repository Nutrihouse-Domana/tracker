import java.util.Properties

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

/** Le uma chave do local.properties (arquivo da maquina do desenvolvedor, fora do Git). */
fun configuracaoLocal(chave: String): String {
    val local = rootProject.file("local.properties")
    if (!local.exists()) return ""

    val props = Properties().apply { local.inputStream().use { load(it) } }
    return props.getProperty(chave).orEmpty()
}

/** Le uma chave do gradle.properties versionado, com `-P<chave>=valor` tendo precedencia. */
fun configuracao(chave: String): String = (project.findProperty(chave) as String?).orEmpty()

/**
 * Credenciais de assinatura: nunca vao para o Git, entao vem do local.properties da maquina que
 * gera o pacote. A linha de comando (`-P`) tem precedencia para permitir build em CI, onde os
 * valores chegam por variavel de ambiente em vez de arquivo.
 */
fun segredoAssinatura(chave: String): String =
    configuracao(chave).ifBlank { configuracaoLocal(chave) }

/**
 * Endereco do backend, amarrado ao tipo de build de proposito:
 *
 * - **release** usa sempre o `gradle.properties` versionado, que aponta para producao;
 * - **debug** usa o `local.properties` da maquina, caindo para producao se nao houver.
 *
 * Se o release respeitasse o `local.properties`, um pacote gerado na maquina de quem estava
 * testando sairia apontando para o backend de laboratorio — falha silenciosa que so apareceria
 * depois de instalada na frota.
 */
val urlProducao = configuracao("tracker.backendBaseUrl")
val urlDesenvolvimento = configuracaoLocal("tracker.backendBaseUrl").ifBlank { urlProducao }

/**
 * Keystore de assinatura do release. O APK e distribuido internamente (fora da Play Store),
 * entao a chave e nossa: sem ela o Android recusa a instalacao. O caminho e as senhas ficam em
 * local.properties, fora do Git — ver docs/agente-android.md.
 *
 * Nulo quando nao configurado: nesse caso nenhuma signingConfig e criada e o assembleRelease
 * falha com mensagem clara, em vez de gerar um APK sem assinatura que nao instala em lugar nenhum.
 */
val arquivoKeystore = segredoAssinatura("tracker.keystoreFile")
    .takeIf { it.isNotBlank() }
    ?.let { rootProject.file(it) }
    ?.takeIf { it.exists() }

android {
    namespace = "br.com.nutrihouse.tracker.agent"
    compileSdk = 34

    defaultConfig {
        applicationId = "br.com.nutrihouse.tracker.agent"
        minSdk = 24
        targetSdk = 34
        versionCode = 1
        versionName = "1.0.0"

        // O codigo de instalacao e digitado pelo tecnico no aparelho, de proposito: um APK que
        // carrega o codigo embutido vira a propria chave da empresa se o arquivo vazar.
        // BACKEND_BASE_URL e definido por buildType (ver abaixo), nao aqui.
    }

    buildFeatures {
        buildConfig = true
        viewBinding = true
    }

    signingConfigs {
        if (arquivoKeystore != null) {
            create("release") {
                storeFile = arquivoKeystore
                storePassword = segredoAssinatura("tracker.keystorePassword")
                keyAlias = segredoAssinatura("tracker.keyAlias")
                keyPassword = segredoAssinatura("tracker.keyPassword")
            }
        }
    }

    buildTypes {
        debug {
            buildConfigField("String", "BACKEND_BASE_URL", "\"$urlDesenvolvimento\"")
        }

        release {
            isMinifyEnabled = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")

            buildConfigField("String", "BACKEND_BASE_URL", "\"$urlProducao\"")
            signingConfig = signingConfigs.findByName("release")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }
}

/**
 * Falha cedo e com mensagem clara se alguem pedir um release sem keystore configurado — o
 * padrao do Gradle seria gerar um APK sem assinatura e so descobrir na hora de instalar.
 *
 * tasks.matching(): a task assembleRelease e criada pelo Android Gradle Plugin depois desta
 * fase, entao tasks.named() falharia com "task not found".
 */
val keystoreConfigurado = arquivoKeystore != null

val urlBackend = configuracao("tracker.backendBaseUrl")

tasks.matching { it.name == "assembleRelease" }.configureEach {
    doFirst {
        if (!keystoreConfigurado) {
            throw GradleException(
                "Release sem keystore. Configure tracker.keystoreFile, tracker.keystorePassword, " +
                    "tracker.keyAlias e tracker.keyPassword em local.properties " +
                    "(ver docs/agente-android.md), ou use ./gradlew :app:assembleDebug para testes.",
            )
        }

        // Somente o build de debug permite texto claro. Um release em http:// instalaria bem e
        // falharia no aparelho com "CLEARTEXT communication not permitted" no primeiro heartbeat.
        if (urlBackend.startsWith("http://")) {
            throw GradleException(
                "Release aponta para HTTP ($urlBackend). O Android bloqueia texto claro fora do " +
                    "build de debug — use https:// em tracker.backendBaseUrl.",
            )
        }
    }
}

dependencies {
    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.appcompat:appcompat:1.7.0")
    implementation("androidx.lifecycle:lifecycle-service:2.8.4")
    implementation("androidx.work:work-runtime-ktx:2.9.1")
    implementation("androidx.security:security-crypto:1.1.0-alpha06")
    implementation("com.google.android.material:material:1.12.0")

    // Localizacao fundida (GPS + rede), muito mais economica que o GPS puro.
    implementation("com.google.android.gms:play-services-location:21.3.0")

    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.8.1")
    implementation("com.squareup.okhttp3:okhttp:4.12.0")

    // Mesmo canal de comandos em tempo real usado pelo agente Windows.
    implementation("com.microsoft.signalr:signalr:8.0.7")
    implementation("org.slf4j:slf4j-simple:2.0.13")
}
