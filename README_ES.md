# Native Unity VOX Reader

[English](README.md)

**La forma más natural y potente de llevar tu arte de MagicaVoxel a Unity.**

[![Versión de Unity](https://img.shields.io/badge/unity-2020.3%2B-blue.svg)](https://unity3d.com/get-unity/download/archive)
[![Licencia](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)

Native Unity VOX Reader es una librería de alto rendimiento e importador de assets que permite tratar los archivos `.vox` de MagicaVoxel como assets nativos de Unity. Sin configuraciones complejas—simplemente arrastra, suelta y disfruta.

---

## Demos

### Importación Instantánea
Arrastra cualquier archivo `.vox` a la ventana de Proyecto de Unity y estará listo para usarse. Genera automáticamente mallas y materiales optimizados.

![Demo de Arrastrar y Soltar](Media~/drag_and_drop.gif)

### Flujo de Trabajo en Tiempo Real
Mantén MagicaVoxel abierto, guarda tus cambios y observa cómo Unity actualiza tus modelos al instante. Da vida a tu proceso creativo.

![Demo de Actualización en Tiempo Real](Media~/realtime_edit.gif)

---

## Características Principales

*   **📦 Plug & Play**: Arrastra archivos `.vox` directamente a tu escena. Unity los trata como prefabs.
*   **🌳 Jerarquía de Escena**: Soporta totalmente las jerarquías de MagicaVoxel (Grupos y Transformaciones).
*   **📐 Alta Optimización**: El algoritmo avanzado de **Greedy Meshing** reduce el conteo de polígonos hasta en un 90% en comparación con métodos basados en cubos.
*   **🎨 Horneado de Texturas**: Hornea todos los colores de los vóxeles en un solo atlas para mantener tus "draw calls" al mínimo.
*   **🛠 Controles en el Inspector**: Ajusta la escala, el tamaño del atlas y la densidad de malla directamente en el importador del asset.
*   **🧩 Minimalista y Limpio**: Cero dependencias externas e incluye Assembly Definitions para tiempos de compilación óptimos.

---

## Empezando

1.  **Instalación**: Copia la carpeta `NativeUnityVoxReader` en tu directorio `Assets`.
2.  **Uso**: 
    - **Automático**: Simplemente arrastra un archivo `.vox` a tu Proyecto.
    - **Tiempo de Ejecución**: Usa el componente `VoxReader` o `ReaderVoxFile.Read()` mediante script.
3.  **Ajustes**: Haz clic en cualquier asset `.vox` en Unity para ajustar su configuración de importación en el Inspector.

---

## 🛠 Estructura del Proyecto

*   **/Runtime**: Lógica principal para el análisis binario y construcción de mallas.
*   **/Editor**: El `ScriptedImporter` que potencia la conversión automática de assets.
*   **/ExampleFiles**: Modelos de muestra para empezar.

---

## 📜 Apoya el Proyecto

Este proyecto es de código abierto y **completamente gratuito**. Lo creé para ayudar a la comunidad a crear cosas increíbles con vóxeles en Unity.

Si esta herramienta te facilitó la vida, considera invitarme a un café. ¡Tu apoyo me ayuda a mantener la librería y seguir creando herramientas para todos!

[**☕ Invítame a un Café**](https://buymeacoffee.com/miventech0)

---
*Creado con pasión por Miventech.*
