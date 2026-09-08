// Tema claro/oscuro.
//
// Vive en JavaScript puro y no en el circuito de Blazor a proposito: el tema es
// una preferencia del navegador de cada persona, no estado de la aplicacion.
// Pasarlo por el circuito significaria un viaje al servidor por cada cambio, y
// dejaria las pantallas de acceso (que son SSR estatico, sin circuito) sin
// posibilidad de cambiarlo.
//
// El arranque (leer la preferencia y ponerla en <html> antes del primer pintado)
// NO esta aqui: va en linea en el <head> de App.razor, porque un archivo externo
// se descarga despues de que el navegador ya pinto la pagina en claro, y se veria
// un destello blanco en cada carga.
window.ccTema = (function () {
    "use strict";

    var CLAVE = "cc-tema";
    var OSCURO = "oscuro";
    var CLARO = "claro";

    function actual() {
        return document.documentElement.getAttribute("data-tema") === OSCURO ? OSCURO : CLARO;
    }

    function pintarBotones(tema) {
        var botones = document.querySelectorAll("[data-cc-tema]");
        for (var i = 0; i < botones.length; i++) {
            botones[i].setAttribute("aria-pressed", String(botones[i].getAttribute("data-cc-tema") === tema));
        }
    }

    // Guarda el tema en la cuenta, para que viaje entre dispositivos. Solo
    // aplica con sesion (App.razor marca data-tema-cuenta) -- en Login/Register
    // no hay cuenta a la cual guardarle nada, y ahi el interruptor ni siquiera
    // aparece. Es "mejor esfuerzo": si la peticion falla (red, sesion vencida),
    // el tema ya se aplico en esta pestana igual, solo no queda guardado para la
    // proxima -- mismo espiritu que el catch de localStorage de abajo.
    function persistirEnCuenta(tema) {
        if (document.documentElement.getAttribute("data-tema-cuenta") !== "1") {
            return;
        }

        var campoToken = document.querySelector("#cc-tema-af input[name='__RequestVerificationToken']");
        if (!campoToken) {
            return;
        }

        var cuerpo = "tema=" + encodeURIComponent(tema) +
            "&__RequestVerificationToken=" + encodeURIComponent(campoToken.value);

        fetch("/perfil/tema", {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            body: cuerpo,
            credentials: "same-origin"
        }).catch(function () { /* red o sesion vencida: se ignora */ });
    }

    function fijar(tema) {
        var valor = tema === OSCURO ? OSCURO : CLARO;

        if (valor === OSCURO) {
            document.documentElement.setAttribute("data-tema", OSCURO);
        } else {
            document.documentElement.removeAttribute("data-tema");
        }

        // Un navegador en modo privado, o con el almacenamiento del sitio
        // bloqueado, lanza al escribir. La preferencia se pierde al recargar,
        // pero la pantalla no debe romperse por eso.
        try {
            localStorage.setItem(CLAVE, valor);
        } catch (e) { /* sin persistencia: el tema dura lo que la pestana */ }

        persistirEnCuenta(valor);
        pintarBotones(valor);
    }

    // Blazor reemplaza el DOM en cada navegacion, asi que los botones nuevos
    // llegan sin su aria-pressed. Se vuelve a pintar despues de cada mejora
    // interactiva y de cada cambio de pagina.
    function sincronizar() {
        pintarBotones(actual());
    }

    document.addEventListener("DOMContentLoaded", sincronizar);

    return { fijar: fijar, actual: actual, sincronizar: sincronizar };
})();
