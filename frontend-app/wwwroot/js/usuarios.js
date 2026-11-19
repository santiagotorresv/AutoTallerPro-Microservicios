(() => {
    "use strict";

    const dialogo = document.getElementById("usuario-estado-dialog");
    let formularioPendiente = null;

    document.querySelectorAll("form[data-confirmar-desactivar]").forEach(formulario => {
        formulario.addEventListener("submit", evento => {
            if (!(dialogo instanceof HTMLDialogElement) || !formulario.dataset.confirmarDesactivar ||
                formulario.dataset.confirmado === "true") {
                return;
            }

            evento.preventDefault();
            formularioPendiente = formulario;
            dialogo.querySelector("[data-usuario-confirmacion]").textContent =
                "Usuario: " + formulario.dataset.confirmarDesactivar;
            dialogo.showModal();
        });
    });

    if (dialogo instanceof HTMLDialogElement) {
        dialogo.querySelector("[data-cancelar-usuario]")?.addEventListener("click", () => dialogo.close());
        dialogo.querySelector("[data-confirmar-usuario]")?.addEventListener("click", () => {
            if (formularioPendiente) {
                formularioPendiente.dataset.confirmado = "true";
                formularioPendiente.requestSubmit();
            }
            dialogo.close();
        });
    }

    document.querySelectorAll("button[data-espera]").forEach(boton => {
        let segundos = Number(boton.dataset.espera);
        const texto = boton.textContent.trim();
        const actualizar = () => {
            if (segundos <= 0) {
                boton.disabled = false;
                boton.textContent = texto;
                clearInterval(temporizador);
                return;
            }
            boton.textContent = texto + " (" + segundos + " s)";
            segundos--;
        };
        const temporizador = setInterval(actualizar, 1000);
        actualizar();
    });
})();
