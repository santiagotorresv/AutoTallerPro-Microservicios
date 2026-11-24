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

    document.querySelectorAll("form[data-usuarios-escritura]").forEach(formulario => {
        formulario.addEventListener("submit", evento => {
            if (evento.defaultPrevented) {
                return;
            }
            if (formulario.dataset.enviando === "true") {
                evento.preventDefault();
                return;
            }

            formulario.dataset.enviando = "true";
            formulario.querySelectorAll("button[type=submit]:enabled").forEach(boton => {
                boton.dataset.envioDeshabilitado = "true";
                boton.disabled = true;
            });
        });
    });

    window.addEventListener("pageshow", evento => {
        if (!evento.persisted) {
            return;
        }
        document.querySelectorAll("form[data-usuarios-escritura]").forEach(formulario => {
            delete formulario.dataset.enviando;
            delete formulario.dataset.confirmado;
            formulario.querySelectorAll("[data-envio-deshabilitado]").forEach(boton => {
                delete boton.dataset.envioDeshabilitado;
                boton.disabled = false;
            });
        });
    });

    document.querySelectorAll("button[data-espera]").forEach(boton => {
        const disponibleDesde = Date.now() + Number(boton.dataset.espera) * 1000;
        const texto = boton.textContent.trim();
        const actualizar = () => {
            const segundos = Math.ceil((disponibleDesde - Date.now()) / 1000);
            if (segundos <= 0) {
                boton.disabled = false;
                boton.textContent = texto;
                clearInterval(temporizador);
                return;
            }
            boton.textContent = texto + " (" + segundos + " s)";
        };
        const temporizador = setInterval(actualizar, 1000);
        actualizar();
    });
})();
