// Interop puntual para interacciones que no puede resolver Blazor por si solo:
// posicionar un elemento segun la medida de otro, y arrastrar-soltar sobre un
// <input type="file">. Nada de esto usa un framework de JS aparte: son globals
// sencillos, en linea con como ya se llama a la API del portapapeles en
// Usuarios.razor.cs (JsRuntime.InvokeVoidAsync("navigator.clipboard...")).

window.ccGastosFicha = {
    // La ficha de detalle (DESIGN.md §5.9) abre a la altura de la fila que la
    // abrio, no siempre arriba del todo de la tarjeta. Si la fila esta cerca del
    // final, el borde inferior de la ficha se alinea con el de la fila para que
    // nunca sobresalga por debajo de la tarjeta.
    posicionar: function (tarjeta, fila, ficha) {
        if (!tarjeta || !fila || !ficha) {
            return;
        }

        // Se vacia antes de medir: si quedo un margen de una apertura anterior, la
        // altura de "ficha" ya lo habria absorbido y el calculo saldria mal.
        ficha.style.marginTop = "0px";

        var tarjetaRect = tarjeta.getBoundingClientRect();
        var filaRect = fila.getBoundingClientRect();
        var fichaAlto = ficha.offsetHeight;

        var offsetFila = filaRect.top - tarjetaRect.top;
        var offsetMaximo = Math.max(0, tarjetaRect.height - fichaAlto);
        var offset = Math.min(Math.max(offsetFila, 0), offsetMaximo);

        ficha.style.marginTop = offset + "px";
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
