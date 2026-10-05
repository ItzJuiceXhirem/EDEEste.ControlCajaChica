// Boton "volver arriba" (Components/Shared/BotonSubirArriba.razor).
//
// JavaScript puro y sin circuito, igual que tema.js: si decidir cuando mostrar
// el boton pasara por Blazor, cada evento de scroll seria un mensaje a SignalR.
// Aqui no hay nada que el servidor necesite saber.
//
// El estado se marca en <html> (data-cc-desplazado) y no en el propio boton:
// Blazor reemplaza el DOM de la pagina al navegar y al hidratar, y un boton
// recien creado hereda el estado por CSS en el acto, sin tener que buscarlo y
// volver a pintarlo. Por la misma razon el clic se atiende por delegacion en
// document y no con un listener sobre el elemento.
window.ccSubirArriba = (function () {
    "use strict";

    var ATRIBUTO = "data-cc-desplazado";

    // Una pantalla completa: a partir de ahi el inicio de la pagina ya quedo
    // fuera de la vista y volver a mano cuesta mas que un giro de rueda. Se mide
    // contra el alto real de la ventana y no con un numero fijo de pixeles para
    // que se sienta igual en un portatil que en un monitor vertical.
    function umbral() {
        return window.innerHeight;
    }

    var pendiente = false;

    function actualizar() {
        pendiente = false;

        if (window.scrollY > umbral()) {
            document.documentElement.setAttribute(ATRIBUTO, "");
        } else {
            document.documentElement.removeAttribute(ATRIBUTO);
        }
    }

    // El evento scroll puede dispararse decenas de veces por fotograma; se
    // agrupan en un solo calculo por fotograma.
    function alDesplazar() {
        if (pendiente) {
            return;
        }

        pendiente = true;
        window.requestAnimationFrame(actualizar);
    }

    function subir(boton) {
        var sinAnimacion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        window.scrollTo({ top: 0, behavior: sinAnimacion ? "auto" : "smooth" });

        // El boton se oculta al llegar arriba, y el foco se perderia con el. Se
        // lleva al titulo de la pagina, que es lo mismo que hace FocusOnNavigate
        // (Routes.razor) al entrar a una pantalla. preventScroll: el foco no
        // debe cortar el desplazamiento suave que acaba de empezar.
        if (document.activeElement !== boton) {
            return;
        }

        var titulo = document.querySelector(".cc-content h1");
        if (!titulo) {
            return;
        }

        if (!titulo.hasAttribute("tabindex")) {
            titulo.setAttribute("tabindex", "-1");
        }

        titulo.focus({ preventScroll: true });
    }

    window.addEventListener("scroll", alDesplazar, { passive: true });

    document.addEventListener("click", function (evento) {
        var boton = evento.target instanceof Element ? evento.target.closest("[data-cc-subir]") : null;
        if (boton) {
            subir(boton);
        }
    });

    // Una recarga con el scroll restaurado por el navegador ya llega desplazada.
    document.addEventListener("DOMContentLoaded", actualizar);

    return { actualizar: actualizar };
})();
