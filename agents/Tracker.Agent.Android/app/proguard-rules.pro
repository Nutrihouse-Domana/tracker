# O SignalR e o Gson que ele usa internamente dependem de reflexao sobre os DTOs.
-keep class br.com.nutrihouse.tracker.agent.dados.** { *; }
-keep class com.microsoft.signalr.** { *; }
-dontwarn com.microsoft.signalr.**

# O DeviceAdminReceiver e resolvido pelo sistema pelo nome declarado no manifest.
-keep class br.com.nutrihouse.tracker.agent.admin.TrackerDeviceAdminReceiver { *; }

-dontwarn org.slf4j.**
