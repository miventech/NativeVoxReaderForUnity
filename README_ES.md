# Native Vox Reader For UNITY

[English](README.md)

**La forma más natural y potente de llevar tu arte de MagicaVoxel a Unity.**

**Aviso:** Esta es una herramienta de terceros creada por la comunidad y NO está afiliada ni respaldada por Unity Technologies. Úsala bajo tu propia responsabilidad.

[![Versión de Unity](https://img.shields.io/badge/unity-2020.3%2B-blue.svg)](https://unity3d.com/get-unity/download/archive)
[![Licencia](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)

Native Vox Reader For UNITY es una librería de terceros, de alto rendimiento y un importador de assets que permite tratar los archivos `.vox` de MagicaVoxel como assets compatibles con Unity. Sin configuraciones complejas—simplemente arrastra, suelta y disfruta.

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

*   **📦 Plug & Play**: Arrastra archivos `.vox` o `.vengi` directamente a tu escena. Unity los trata como prefabs.
*   **🌳 Jerarquía de Escena**: Soporta totalmente las jerarquías de MagicaVoxel (Grupos y Transformaciones).
*   **📐 Alta Optimización**: El algoritmo avanzado de **Greedy Meshing** reduce el conteo de polígonos hasta en un 90% en comparación con métodos basados en cubos.
*   **🎨 Horneado de Texturas**: Hornea todos los colores de los vóxeles en un solo atlas para mantener tus "draw calls" al mínimo.
*   **� Renderizado Dinámico**: Nuevo sistema extensible que permite diferentes modos de renderizado (Atlas Horneado vs. Estilo Paleta).
*   **🚀 Auto-Descubrimiento**: Añade nuevos algoritmos de renderizado simplemente heredando de `VoxRenderAbstract` y aparecerán en el Inspector automáticamente.
*   **🛠 Controles en el Inspector**: Interfaz de usuario totalmente dinámica que se adapta al modo de renderizado seleccionado.
*   **🧩 Minimalist y Limpio**: Cero dependencias externas e incluye Assembly Definitions para tiempos de compilación óptimos.
*   **🔄 Soporte para Vengi**: Ahora incluye soporte para leer e importar datos desde Vengi.

---

## Créditos

Las contribuciones a este proyecto son bienvenidas y apreciadas. ¡Gracias a todos los que ayudan a mejorar Native Vox Reader!

### Colaboradores

*   **[aqscithe](https://github.com/alleriumlabs)** (fork [alleriumlabs/NativeVoxReader](https://github.com/alleriumlabs/NativeVoxReader)) — Aportaciones:
    *   **Lectura de rotaciones y grupos**: Mejora del parsing de nodos `nTRN` para que las transformaciones de MagicaVoxel (rotaciones y grupos) se lean correctamente.
    *   **Soporte de animaciones**: Añadido `VoxAnimation` (Runtime/Data) con mejoras en la lectura de animaciones.

---

## 🚀 Próximas Características (Roadmap)

*   **🎬 Sistema de Animación**: Actualmente estamos desarrollando un sistema de animación nativo para vóxeles para dar vida a tus personajes directamente dentro de la herramienta.
*   **⚡ Optimización Continua**: Mejoras constantes de rendimiento para escenas a gran escala.
*   **🛠 Herramientas de Edición Avanzadas**: Más herramientas integradas para la manipulación de vóxeles dentro del Editor de Unity.

---

## Empezando

1.  **Instalación**:
    - **Opción A (Package Manager - Recomendado)**: 
        1. En Unity, ve a `Window` > `Package Manager`.
        2. Haz clic en el botón `+` y selecciona `Add package from git URL...`.
        3. Pega: `https://github.com/miventech/NativeUnityVoxReader.git`
    - **Opción B (Manual)**: Copia la carpeta `NativeUnityVoxReader` en tu directorio `Assets`.
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
*Creado con pasión por Miventech. Eso y Necesitaba esta herramienta para otro proyecto jejeje 😏😉*
