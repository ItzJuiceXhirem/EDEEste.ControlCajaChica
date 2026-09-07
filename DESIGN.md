# DESIGN.md

Sistema de diseño de **Control de Caja Chica**. Es la referencia para cualquier pantalla nueva o reskin: paleta, tipografía, espaciado/radios y el tono general. El objetivo es que la app deje de verse como el scaffold por defecto de Blazor/Bootstrap y pase a tener una identidad propia, sin perder la seriedad que exige una herramienta de manejo de dinero de empresa.

**Estado de partida:** hoy la app usa Bootstrap 5.3.3 sin personalizar (`btn-primary` sigue en el azul `#1b6ec2` del scaffold, el sidebar sigue con el degradado morado por defecto). No hay nada de paleta "existente" que preservar más allá de esa base de Bootstrap — este documento es la primera decisión de identidad real del proyecto.

---

## 1. Paleta de colores

Paleta base elegida: **Regal Navy** (azules profundos + dorado/amarillo escolar + marfil). Los colores de estado (éxito/peligro/advertencia/info) se dejan en los valores por defecto de Bootstrap **a propósito** — ver la nota al final de esta sección.

### 1.1 Marca (primarios)

| Color | Hex | Rol | Dónde se usa |
|---|---|---|---|
| **Ink Black** | `#000814` | Extremo más oscuro de la marca | Fin del degradado del sidebar; texto de máximo contraste sobre fondos claros cuando el gris de Bootstrap se queda corto |
| **Prussian Blue** | `#001D3D` | Primario — variante oscura / hover | `btn-primary:hover`/`:active`, encabezado y pie de modales (reemplaza el `#002a5f` ad-hoc que hoy tiene el modal de observaciones en Arqueos), punto medio del degradado del sidebar |
| **Regal Navy** | `#003566` | **Primario** | `btn-primary` en reposo, enlaces (`a`, `.btn-link`), `--bs-primary`, anillo de foco, encabezados de tablas si se necesita color de fondo |

Los tres azules forman una sola familia tonal (claro→oscuro) pensada para poder degradar sin que se note el salto: `#003566 → #001D3D → #000814`.

### 1.2 Acento (secundarios)

| Color | Hex | Rol | Dónde se usa |
|---|---|---|---|
| **School Bus Yellow** | `#FFC300` | Acento principal | Botones secundarios de énfasis ("acción recomendada" que no es la primaria), resaltar el total en tablas de dinero, indicador de fila activa en el nav |
| **Bright Gold** | `#FFDA20` | Acento claro | Hover del acento, fondos tenues de tarjetas destacadas (`background-color` al 10-15% de opacidad), no usar en texto sobre blanco por contraste bajo |

**Nota de coherencia:** el acento dorado cae en la misma familia tonal que `--bs-warning` (`#ffc107`) de Bootstrap. Es intencional — significa que un badge `text-bg-warning` (p. ej. "Pendiente de reposición") ya va a armonizar con el acento de marca sin tocarlo, en vez de chocar con él.

### 1.3 Neutros

| Color | Hex | Rol |
|---|---|---|
| **Ivory** | `#FFFDF5` | Fondo general de la app (`body`), en vez del blanco puro por defecto — le da calidez sin sacrificar legibilidad. Aclarado desde el `#FFFCEB` original de la paleta Regal Navy tras revisar los mockups de Inicio: se ve más limpio sin llegar a blanco puro |
| Blanco | `#FFFFFF` | Superficies elevadas: `.card`, `.modal-content` (cuerpo), filas de tabla claras |
| Escala de grises de Bootstrap (`--bs-gray-100`…`--bs-gray-900`) | sin cambio | Texto secundario, bordes, `text-muted`, divisores — no se tocan; la paleta de marca es para *chrome*, no para la escala de grises de UI |

### 1.4 Estados — se mantienen los valores de Bootstrap

| Estado | Variable Bootstrap | Hex | Uso ya existente en el código |
|---|---|---|---|
| Éxito | `--bs-success` | `#198754` | `text-bg-success` (Repuesto, Cuadrado), `btn-success` |
| Peligro | `--bs-danger` | `#dc3545` | `text-bg-danger` (Anulado, Faltante, Rechazado), `alert-danger`, `btn-danger` |
| Advertencia | `--bs-warning` | `#ffc107` | `text-bg-warning` (Pendiente de reposición), `alert-warning` |
| Info | `--bs-info` | `#0dcaf0` | `text-bg-info` (En proceso de reposición, Sobrante) |

**Por qué no se reemplazan con la paleta nueva:** verde/rojo/amarillo/celeste ya cargan un significado semántico que toda la app usa para comunicar estado a simple vista (dinero repuesto = verde, faltante = rojo, etc.). Introducir el azul/dorado de marca ahí rompería esa lectura instantánea. La paleta de marca vive en los elementos de *identidad* (botones primarios, navegación, encabezados) — los de *estado* siguen siendo universales.

### 1.5 Cómo aplicarla (nota de implementación)

Bootstrap 5.3 expone estos colores como variables CSS (`--bs-primary`, `--bs-btn-*`, etc.), así que la forma correcta de aplicar la marca es sobreescribir esas variables en `wwwroot/app.css` (o un `design-tokens.css` nuevo importado después de Bootstrap), **no** editar `lib/bootstrap/dist/css/bootstrap.min.css` directamente — ese archivo es de terceros y se pisaría en la próxima actualización de la librería.

---

## 2. Tipografía

| Familia | Uso | Fuente |
|---|---|---|
| **Stack Sans Headline** | Títulos y encabezados (`h1`–`h6`, `.navbar-brand`, cabeceras de tarjeta) | [Google Fonts](https://fonts.google.com/specimen/Stack+Sans+Headline) |
| **Stack Sans Text** | Cuerpo de texto, formularios, tablas, botones | [Google Fonts](https://fonts.google.com/specimen/Stack+Sans+Text) |

Ambas son parte de la misma superfamilia (Stack Sans), pensada explícitamente para pareja titular/texto — no hace falta buscar una segunda fuente para contraste, el contraste ya viene dado por el corte expresivo de la variante Headline frente al corte utilitario de Text.

### 2.1 Carga

Vía `<link>` de Google Fonts en `App.razor` (dentro de `<head>`, antes de `app.css`), con `font-display: swap` para no bloquear el primer render:

```html
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Stack+Sans+Headline:wght@500;600;700&family=Stack+Sans+Text:wght@400;500;600&display=swap" rel="stylesheet">
```

(Pesos exactos disponibles a confirmar en la página de cada fuente — arriba se listan los que cubren la escala de la sección 2.2.)

### 2.2 Escala y pesos

Mapeada 1:1 sobre lo que Bootstrap ya trae (`--bs-body-font-size: 1rem`), para no reescribir el sistema de tipos de Bootstrap, solo sustituir la familia y afinar pesos:

| Elemento | Familia | Tamaño | Peso |
|---|---|---|---|
| `h1` | Stack Sans Headline | 2.5rem | 700 (Bold) |
| `h2` | Stack Sans Headline | 2rem | 600 (Semibold) |
| `h3` / `h2.h5`, `h2.h6` en las pantallas actuales | Stack Sans Headline | 1.25–1.5rem | 600 (Semibold) |
| `.navbar-brand` | Stack Sans Headline | 1.1rem (ya fijado en `NavMenu.razor.css`) | 600 (Semibold) |
| Cuerpo (`body`, `p`, `td`, `label`) | Stack Sans Text | 1rem | 400 (Regular) |
| Botones (`.btn`) | Stack Sans Text | 1rem | 500 (Medium) — un poco más de peso que el cuerpo para que un CTA se distinga sin gritar |
| `.form-text`, `small`, pies de tabla | Stack Sans Text | 0.875rem | 400 (Regular) |
| Datos monetarios (`RD$ ...` en tablas y tarjetas) | Stack Sans Text | igual que el contexto | 600 (Semibold) — un monto siempre debe leerse antes que el texto que lo rodea |

**Fallback stack** (mientras cargan las fuentes web, o si Google Fonts no está disponible): reutilizar el stack de sistema que Bootstrap ya trae por defecto, en vez de inventar uno nuevo:

```css
font-family: "Stack Sans Text", system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
```

---

## 3. Espaciado y radios de borde

No se introduce una escala nueva — la app ya usa consistentemente las utilidades de espaciado de Bootstrap (`mb-2`, `mb-3`, `gap-2`, `px-4`, `row`/`col-md-*`) en todas las pantallas existentes, así que el sistema de diseño *documenta* esa escala en vez de reemplazarla.

### 3.1 Espaciado

Escala de Bootstrap (`$spacer = 1rem`, con pasos `0`–`5`):

| Clase | Valor | Uso típico ya visto en el código |
|---|---|---|
| `*-1` | 0.25rem (4px) | Separación mínima entre un ícono y su etiqueta |
| `*-2` | 0.5rem (8px) | Entre un `<label>` y su `<input>`, entre botones de una misma fila de acciones |
| `*-3` | 1rem (16px) | Entre secciones de un formulario (`mb-3` es el más usado en `RegistrarGasto.razor`, `Fondos.razor`, etc.) |
| `*-4` | 1.5rem | Padding lateral del contenido principal (`article.content.px-4`) |
| `*-5` | 3rem | Separación entre bloques grandes de una página (raramente usado hoy; reservar para separar secciones muy distintas) |

Regla práctica: `mb-2` dentro de un mismo control/etiqueta, `mb-3` entre campos de un formulario, `gap-2` entre botones de una barra de acciones.

### 3.2 Radios de borde

También se hereda la escala de Bootstrap, sin overrides:

| Variable | Valor | Uso |
|---|---|---|
| `--bs-border-radius-sm` | 0.25rem (4px) | Badges pequeños, inputs compactos |
| `--bs-border-radius` (default) | 0.375rem (6px) | Botones, inputs, `.card`, `.modal-content` — el radio por defecto para casi todo |
| `--bs-border-radius-lg` | 0.5rem (8px) | Tarjetas grandes o modales cuando se quiera un poco más de suavidad (p. ej. el modal de observaciones de Arqueos) |
| `--bs-border-radius-pill` | 50rem | Badges de estado (`text-bg-*`) si en algún momento se quiere un look más redondeado que el actual `rounded` cuadrado de Bootstrap — hoy no se usa, queda como opción |

No hay necesidad de una escala propia: el radio de 6px por defecto ya es coherente con un tono serio/institucional (más cuadrado que redondeado, sin llegar a las esquinas totalmente rectas).

---

## 4. Tono y dirección estética

**En una frase:** confiable y serio primero, cálido segundo — es una herramienta donde se audita dinero de la empresa, no un producto de consumo.

- **Institucional sin ser frío.** El azul marino profundo (`#003566`/`#001D3D`) comunica seriedad y control — apropiado para un sistema que firma cada fila con HMAC y lleva una cadena de auditoría. El acento dorado (`#FFC300`/`#FFDA20`) es lo que evita que se sienta burocrático: se usa con moderación, solo donde de verdad hay que llamar la atención (un monto, una acción recomendada), nunca como color de fondo grande.
- **Los datos mandan sobre la decoración.** Es una app de tablas, montos y estados — la tipografía y el color existen para que un monto, un estado o un botón de acción se lean rápido, no para llamar la atención sobre sí mismos. De ahí el peso semibold en los montos (sección 2.2) y que el acento dorado nunca se use en texto de cuerpo.
- **Los colores de estado son sagrados.** Verde/rojo/amarillo/celeste (sección 1.4) no se tocan nunca por motivos estéticos — son el lenguaje visual con el que un Gerente o Auditor distingue "todo bien" de "hay que revisar esto" en una tabla larga, y ese lenguaje ya está en uso en producción.
- **Consistente con lo que ya existe, no una reinvención.** Este documento formaliza colores y tipografía nuevos, pero deja intactas las convenciones de layout, espaciado y componentes de Bootstrap que ya rigen toda la app (formularios `EditForm`, tablas `table-striped`, tarjetas `.card`, badges `text-bg-*`). Un reskin implementado a partir de este documento no debería cambiar la estructura de ninguna pantalla, solo su piel visual.

---

## 5. Componentes y patrones adicionales

Patrones que salieron de las rondas de mockups de **Inicio**, **Arqueos**, **Iniciar sesión / Solicitar acceso** y **Gastos / Registrar gasto** (explorados como Artifacts, todavía no implementados en código). Cada uno quedó resuelto a nivel de diseño — esta sección es la especificación para cuando se construyan.

### 5.1 Secciones plegables con control por rol

Patrón para cualquier pantalla que combine "un formulario de captura" con "una lista/historial debajo" (el caso concreto: Arqueos, donde el conteo nuevo y el historial compiten por el mismo scroll).

- Las secciones de captura (p. ej. "Nuevo arqueo" + "Resumen") se pliegan a una sola franja mediante una flechita a la derecha del control más a la derecha de la cabecera (junto al selector de fecha, no junto al título) — un solo control pliega/despliega todas las secciones relacionadas a la vez, no una por una.
- **Colapsada, la franja no queda vacía**: conserva el nombre de la sección, un resumen de una línea del último valor capturado (p. ej. "Contado RD$ 18,682.00 · Teórico RD$ 18,682.00") y, a la derecha, el badge de resultado con el mismo aspecto que usa en el historial (ver 5.2). Así minimizar no hace perder el contexto de lo que se acaba de hacer.
- **Quien no tiene el permiso de captura nunca ve la sección abierta.** En vez de la flecha, lleva un ícono de candado (no interactivo) y el texto de resumen se sustituye por la razón ("Sólo el Custodio puede registrar un arqueo") — el mismo espíritu que el botón deshabilitado-con-tooltip que ya existe en `Reposiciones.razor` para Finanzas, pero en su variante "sección entera", no solo un botón.
- El punto de plegado importa: en una pantalla con formulario + historial, dimensionar las secciones para que el título de la lista de abajo (p. ej. "Historial de arqueos") ya sea visible sin hacer scroll, aunque el usuario no haya plegado nada — es la pista de que hay más contenido debajo.

### 5.2 Tablas de historial / auditoría

- **El borde de fila va en la fila completa (`border-bottom` en el contenedor de la fila), nunca en cada celda por separado.** Poner el borde por celda produce un desnivel visible en cuanto una celda es más alta que sus vecinas (un badge de estado es más alto que texto plano) — cada borde cae a una altura distinta. Con el borde a nivel de fila, todas quedan parejas sin importar el contenido de cada celda.
- **Texto largo se trunca a 60 caracteres con "…"**, y solo si se truncó aparece un botón de ojo en su propia columna (no dentro de la celda de texto) para abrir el detalle completo en un modal (ver 5.3). Si el texto ya cabe en 60 caracteres, no hay ojo — no todas las filas necesitan la misma acción.
- El botón que expande un desglose en línea (p. ej. "Ver desglose") es el mismo botón que lo oculta — su etiqueta cambia a "Ocultar" cuando está expandido. No se agrega un segundo control de "cerrar" dentro del panel expandido; sería un control redundante para la misma acción.
- Orden de columnas: los identificadores primero (fecha, estado/resultado), luego los montos de izquierda a derecha en el mismo orden en que se usan en el cálculo que los relaciona, el texto libre (observaciones) después de los montos, y las acciones (ver detalle, expandir) al final — nunca intercaladas entre columnas de datos.
- **Nota técnica — contención horizontal:** toda tabla de este tipo vive dentro de un contenedor con `overflow-x: auto` propio (nunca a nivel de página), aunque hoy quepa sin scroll. Es la garantía dura contra que una columna nueva, un dato más largo de lo previsto, o simplemente una pantalla angosta hagan que la fila —y con ella los botones de la columna de acciones— se salga por el costado de la tarjeta. El texto largo se trunca con `text-overflow: ellipsis` sobre un `max-width` fijo por columna (ver la regla de los 60 caracteres arriba) precisamente para no depender de que el contenedor crezca: contención y truncado son dos capas de la misma protección, no alternativas entre sí.

### 5.3 Modal con degradado de marca

Reemplaza el modal de observaciones de Arqueos (antes en `#002a5f` sólido) y es el patrón a seguir para cualquier modal que necesite destacar sobre el resto de la pantalla:

- **Encabezado + franja de metadatos fusionados en un solo bloque** con degradado diagonal esquina-a-esquina (`linear-gradient(to bottom right, ...)`, nunca `135deg` en una caja ancha y baja — ese ángulo recorre casi todo su rango en horizontal y se ve como un degradado lateral, no diagonal): **Regal Navy** en la esquina superior izquierda → **Prussian Blue** al centro → **Ink Black** en la esquina inferior derecha. El pie del modal (donde va el botón de cerrar) usa el mismo degradado a menor escala, aunque no sea una superficie contigua al encabezado.
- El texto sobre el degradado usa Ivory o los dorados (`#FFDA20` para etiquetas, `#FFFDF5` para valores) — nunca los grises que se usan sobre fondo claro.
- **Divisor entre el degradado y el cuerpo blanco**: una franja fina (2–3px) con degradado horizontal de los dos dorados y un toque de Ivory al centro (`linear-gradient(90deg, #FFC300, #FFDA20, #FFFDF5, #FFDA20, #FFC300)`) en vez de una línea sólida — es donde el sistema mete el amarillo junto al azul sin que compitan.
- **Los badges de estado (`text-bg-*`) mantienen su aspecto normal** (fondo claro, texto de color) incluso dentro del degradado oscuro — no se invierten. Lo único que sí necesita aclararse es el **monto crudo** cuando lleva el color de estado directamente en el texto (p. ej. una diferencia negativa en rojo): el rojo/celeste normal de Bootstrap pierde casi todo el contraste sobre el azul oscuro, así que ahí se usa una variante clara (`#FF9AA3` en vez de `#dc3545` para texto de peligro sobre fondo oscuro) — la única excepción documentada a la regla de la sección 1.4 de no tocar los colores de estado, y solo aplica a texto suelto sobre fondo oscuro, nunca a los badges.

### 5.4 Pantallas de acceso (Login / Register)

Iniciar sesión y Solicitar acceso son las únicas pantallas sin sidebar — son la puerta de entrada, no una vista de trabajo — y llevan el tratamiento más oscuro y con más presencia del dorado de toda la app:

- Fondo negro tinta (`#000814`) o degradado navy→negro, nunca el marfil que usa el resto de la app.
- Una banda o filo dorado como gesto de marca (una diagonal, un filete entre dos paneles, o el borde superior de la tarjeta del formulario) — es donde el acento dorado tiene más licencia que en cualquier otra pantalla, precisamente porque aquí no compite con datos ni con tablas.
- **Campo de fondo con las denominaciones**: fichas sueltas de billete (rectangulares) y moneda (circulares) con el signo `$` delante del número (`$2000`, `$500`, `$25`…), dispersas con rotaciones aleatorias pequeñas (±15°) y en un tono muy tenue (opacidad del trazo/texto entre 15-45%) para que queden de textura, no de contenido — la atmósfera sale del propio producto (son las mismas denominaciones de la calculadora de Arqueos) en vez de un patrón decorativo genérico. Un degradado radial oscuro por encima atenúa el campo detrás del panel del formulario para no competir con los campos.
- Los estados de error (`ValidationMessage`, `StatusMessage`) y el aviso informativo ("sistema sin Administrador") siguen la misma regla de la sección 5.3: el rojo y el celeste se aclaran para el fondo oscuro, nunca se sustituyen por otro color.
- Register enlaza de vuelta a Login ("¿Ya tiene una cuenta? Iniciar sesión") y Login enlaza a Register ("¿No tiene cuenta? Solicitar acceso al sistema") — ninguna de las dos debe ser una puerta sin salida.

### 5.5 Barra superior de contexto

Toda pantalla interior (no las de acceso, ver 5.4) lleva una barra fija entre el sidebar y el contenido, con dos elementos siempre en los mismos extremos: a la izquierda el nombre de la sección en versalitas espaciadas (mismo tratamiento tipográfico que las etiquetas de campo — mayúsculas pequeñas, `letter-spacing` abierto), a la derecha la fecha completa en formato largo ("21 de agosto de 2026").

- Es **blanca**, no Ivory como el fondo general de la app (sección 1.3) — la diferencia de tono es lo que separa visualmente el *marco* de la pantalla (sidebar + esta barra) del *contenido* que sí vive sobre Ivory.
- Nació en la ronda de mockups de Gastos, pero se decidió aplicarla **a todas las pantallas por igual**, no solo donde se diseñó primero: un marco que cambia de una pantalla a otra se siente como una inconsistencia, no como una decisión.

### 5.6 Indicador de navegación activa (sidebar)

El ítem activo del menú lateral no lleva un filete corto centrado — lleva el **borde izquierdo completo de la pastilla**, de arriba a abajo, con la esquina redondeada al mismo radio que la propia pastilla (radio asimétrico: redondeado hacia el lado del texto, recto contra el borde del sidebar, para que no quede un hueco entre el borde de la pastilla y el borde del panel).

Un filete corto centrado se lee como una marca de posición entre varias opciones; el borde de altura completa se lee como "esta fila entera está encendida", que es la lectura correcta cuando solo una opción de navegación puede estar activa a la vez. Color: **School Bus Yellow** (`#FFC300`) sobre el fondo del ítem activo (`Prussian Blue` o una variante más clara del mismo azul — nunca el degradado del sidebar completo, que ya está ocupado marcando profundidad, no selección).

### 5.7 Badges de estado en píldora

Los badges de estado (Pendiente, Repuesto, Cuadrado, etc.) usan forma de **píldora con fondo tenue**, no la variante sólida de Bootstrap (`text-bg-success`, fondo lleno + texto blanco). Los hues siguen respondiendo a la semántica de la sección 1.4 — este patrón no cambia qué color significa qué, cambia la *construcción* del badge.

La paleta final mezcla dos fuentes a propósito, cada una verificada contra el mínimo de contraste WCAG AA (4.5:1 para texto de este tamaño):

| Estado | Casos de uso | Origen | Fondo | Texto | Contraste |
|---|---|---|---|---|---|
| Éxito | Repuesto, Cuadrado | hex propio | `#DCF3E7` | `#146C43` | 5.53:1 (AA) |
| Peligro | Anulado, Rechazado, Faltante | hex propio | `#FBE0E3` | `#B02A37` | 5.22:1 (AA) |
| Oscuro | Anulación pendiente / por confirmar | hex propio | `#E4E7EB` | `#343A40` | 9.27:1 (AAA) |
| Advertencia | Pendiente de reposición | `bg-warning-subtle` + `text-warning-emphasis` | `#fff3cd` | `#664d03` | 7.21:1 (AAA) |
| Info | En proceso, Sobrante | `bg-info-subtle` + `text-info-emphasis` | `#cff4fc` | `#055160` | 7.65:1 (AAA) |

Siempre con `rounded-pill`.

- **Por qué la mezcla:** Éxito, Peligro y Oscuro son la paleta de un mockup hecho a mano (el artifact de diseño no puede cargar Bootstrap) que se prefirió sobre el equivalente de Bootstrap una vez comparados lado a lado — y los tres pasan el mínimo de accesibilidad tal cual están, así que no había razón para tocarlos. Advertencia e Info se dejaron en las utilidades nativas de Bootstrap 5.3 porque la versión hecha a mano de Info **fallaba** el mínimo (4.36:1, por debajo de 4.5:1) y la de Advertencia estaba justa (5.58:1); usar Bootstrap ahí resuelve las dos sin inventar un tono nuevo, y de paso deja el conjunto con un rango de contraste más parejo (5.22:1–9.27:1, contra el 4.36:1–9.27:1 de la paleta 100% a mano).
- Los tres hex de la tabla son la única excepción documentada a "usar las utilidades de Bootstrap" en todo este documento. Si se agrega un sexto estado semántico más adelante, empezar por las utilidades `-subtle`/`-emphasis` de Bootstrap para ese color — no sumar un cuarto hex a mano sin la misma verificación de contraste que llevó a estos tres.

### 5.8 Filtros de lista: cifras y buscador

Para cualquier pantalla que combine un resumen numérico con una tabla debajo (el caso de Gastos: pendiente / en proceso / repuesto / anulado), el resumen puede funcionar como el propio control de filtro, en vez de un `<select>` aparte:

- Cada cifra es una tarjeta compacta con **el color semántico en el borde superior** (3px), no de fondo ni de relleno — coherente con que el color de estado es información, no decoración (sección 4). El número va en Stack Sans Headline; el conteo de registros debajo, en texto secundario pequeño.
- Tocar una cifra filtra la tabla por ese estado. La cifra activa se marca con un anillo interior en Regal Navy (`box-shadow` hacia adentro), nunca con relleno — un relleno competiría con el color semántico del borde.
- Una cifra puede en cambio **abrir una vista distinta** en vez de filtrar la misma tabla, cuando su información no cabe en las columnas estándar (en Gastos, "Anulado" abre una vista aparte con las solicitudes por confirmar y el historial, que traen sus propias columnas — motivo, quién decide — que no aplican a un gasto pendiente).
- El buscador de texto libre de la tabla se ancla a la derecha del título de la tarjeta y **crece hacia la izquierda con `white-space: nowrap`** en vez de partirse en dos líneas al no caber; lleva un ícono de lupa de trazo (no relleno) que hereda el color del propio campo, igual que cualquier ícono con `currentColor` (ver 5.11).

### 5.9 Ficha de detalle desplegable

Alternativa a expandir la fila in situ (sección 5.2) o a un modal (sección 5.3) para cuando el usuario necesita ver el detalle de una fila **sin perder de vista las demás** — un modal tapa la tabla; esta ficha no:

- Al tocar una fila, la tabla cede ancho: pasa de usar todas las columnas a un subconjunto más corto que sigue sirviendo para comparar entre filas (fecha, la parte que identifica el registro, monto, estado), y a la derecha aparece una ficha con el detalle completo.
- La cabecera de la ficha usa el mismo degradado esquina-a-esquina de marca de la sección 5.3, filete de dos dorados incluido — es el mismo lenguaje visual que ya significa "esto es información destacada", aplicado a un panel en vez de a un modal.
- Se cierra tocando la misma fila otra vez, o el botón **✕** de la cabecera de la ficha — los dos mecanismos coexisten a propósito (el mismo espíritu de "un solo control hace y deshace" que ya rige el botón "Ver desglose ↔ Ocultar" de 5.2), nunca se exige usar uno en particular.
- El patrón no obliga a que la ficha sea editable: puede ser de solo lectura con acciones (como en Gastos, donde el concepto no se edita desde ahí) o permitir edición — cada pantalla decide eso por separado.

### 5.10 La marca: el cofre

Ícono propio para acompañar "Control de Caja Chica" donde haga falta una marca gráfica (pantallas de acceso, sidebar, favicon eventual): un cofre visto de frente, con tapa abovedada, correas verticales y una cerradura, y una moneda con el signo `$` entrando por la ranura de la tapa, insertada aproximadamente una cuarta parte (no flotando encima ni completamente adentro) — para que se lea "moneda entrando al fondo" de un vistazo. Trazo (`stroke`), no relleno, en **School Bus Yellow** sobre los fondos oscuros de la sección 5.4, y también sobre el fondo oscuro del sidebar (sección 5.6) junto al nombre de la app.

### 5.11 Nota técnica: íconos SVG reutilizados con `<use>`

Un ícono definido una vez como `<symbol>` y reutilizado con `<use href="#id">` se clona en un **shadow tree**: un selector CSS descendente escrito desde fuera (p. ej. `.mark .trazo { stroke: ... }`) no lo alcanza y el ícono cae a los valores por defecto de SVG (relleno negro sólido). Las propiedades que **sí heredan** hacia dentro del clon son `color`, `font-family` y similares — por eso el patrón correcto es dibujar el símbolo con `fill="currentColor"` / `stroke="currentColor"` y fijar el color únicamente en el elemento que contiene el `<use>` (`.mark { color: #FFC300 }`), nunca con un selector que apunte a las partes internas del símbolo.
