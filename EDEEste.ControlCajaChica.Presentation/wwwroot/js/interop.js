// Interop puntual para interacciones que no puede resolver Blazor por si solo:
// posicionar un elemento segun la medida de otro, y arrastrar-soltar sobre un
// <input type="file">. Nada de esto usa un framework de JS aparte: son globals
// sencillos, en linea con como ya se llama a la API del portapapeles en
// Usuarios.razor.cs (JsRuntime.InvokeVoidAsync("navigator.clipboard...")).

// El marco interior de la app se dibuja con CSS `zoom` (ver --cc-zoom en
// app.css), y eso complica cualquier medida hecha con getBoundingClientRect:
// esa API devuelve pixeles YA multiplicados por el zoom acumulado, pero un
// valor que se ASIGNE despues por JS a una propiedad CSS de un elemento que
// vive dentro de ese mismo zoom se interpreta como valor local y el navegador
// lo multiplica otra vez al pintar. Sin corregirlo, cualquier "margin-top"
// calculado a partir de una medida quedaria un 10% mas grande de lo debido.
function ccZoomAcumulado(el) {
    var factor = 1;
    var nodo = el;

    while (nodo && nodo.nodeType === 1) {
        var z = getComputedStyle(nodo).zoom;
        var n = parseFloat(z);

        if (!isNaN(n) && n > 0) {
            factor *= n;
        }

        nodo = nodo.parentElement;
    }

    return factor;
}

window.ccGastosFicha = {
    _observador: null,

    // El calculo puro: se separa de "posicionar" porque hace falta repetirlo
    // mas de una vez (ver comentario del ResizeObserver abajo).
    _calcular: function (tarjeta, fila, ficha) {
        // Se vacia antes de medir: si quedo un margen de una apertura anterior, la
        // altura de "ficha" ya lo habria absorbido y el calculo saldria mal.
        ficha.style.marginTop = "0px";

        var tarjetaRect = tarjeta.getBoundingClientRect();
        var filaRect = fila.getBoundingClientRect();
        var fichaAlto = ficha.offsetHeight;

        // Todo lo de arriba esta en pixeles visuales (ya multiplicados por el
        // zoom). El clamp se hace entero en esa misma unidad, asi que sigue
        // siendo correcto -- solo el numero final que se ASIGNA como estilo
        // necesita volver a la unidad local (ver ccZoomAcumulado arriba).
        var offsetFila = filaRect.top - tarjetaRect.top;
        var offsetMaximo = Math.max(0, tarjetaRect.height - fichaAlto);
        var offsetVisual = Math.min(Math.max(offsetFila, 0), offsetMaximo);

        var zoom = ccZoomAcumulado(ficha);
        ficha.style.marginTop = (offsetVisual / zoom) + "px";
    },

    // La ficha de detalle (DESIGN.md §5.9) abre a la altura de la fila que la
    // abrio, no siempre arriba del todo de la tarjeta. Si la fila esta cerca del
    // final, el borde inferior de la ficha se alinea con el de la tarjeta para
    // que nunca sobresalga por debajo.
    posicionar: function (tarjeta, fila, ficha) {
        if (!tarjeta || !fila || !ficha) {
            return;
        }

        if (this._observador) {
            this._observador.disconnect();
        }

        this._calcular(tarjeta, fila, ficha);

        // Stack Sans carga con font-display:swap: el primer calculo se hace con
        // la fuente de reemplazo del sistema, y el texto de la ficha (un
        // concepto largo, por ejemplo) puede reacomodarse -- y crecer un poco --
        // en cuanto la tipografia real termina de descargarse. El
        // ResizeObserver reacciona a ESE cambio de alto y vuelve a calcular, en
        // vez de adivinar cuanto tarda la fuente. No entra en bucle: el propio
        // "margin-top" que este metodo asigna no forma parte de la caja que el
        // observador vigila (solo mide content/border-box, nunca el margen).
        var ficha2 = ficha;
        var tarjeta2 = tarjeta;
        var fila2 = fila;
        var self = this;

        this._observador = new ResizeObserver(function () {
            self._calcular(tarjeta2, fila2, ficha2);
        });
        this._observador.observe(ficha);
    }
};

window.ccDragDrop = {
    // InputFile de Blazor escucha el evento "change" nativo de su <input>. Aqui se
    // le asignan los archivos soltados y se dispara ese mismo evento, para no
    // duplicar en JS la logica de C# que ya procesa la seleccion por clic.
    wire: function (zona, input) {
        if (!zona || !input || zona.dataset.ccWired === "1") {
            return;
        }

        zona.dataset.ccWired = "1";

        var evitarPropagacion = function (evento) {
            evento.preventDefault();
            evento.stopPropagation();
        };

        ["dragenter", "dragover", "dragleave", "drop"].forEach(function (nombre) {
            zona.addEventListener(nombre, evitarPropagacion);
        });

        zona.addEventListener("dragover", function () {
            zona.classList.add("cc-drop-over");
        });

        zona.addEventListener("dragleave", function () {
            zona.classList.remove("cc-drop-over");
        });

        zona.addEventListener("drop", function (evento) {
            zona.classList.remove("cc-drop-over");

            var archivos = evento.dataTransfer && evento.dataTransfer.files;
            if (!archivos || archivos.length === 0) {
                return;
            }

            input.files = archivos;
            input.dispatchEvent(new Event("change", { bubbles: true }));
        });
    }
};
