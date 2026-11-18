(() => {
    "use strict";

    const filtros = document.getElementById("hu3AutoFilterForm");
    const estado = document.getElementById("usuarios-busqueda-estado");

    if (filtros && estado) {
        const informar = () => {
            estado.textContent = "Sin conexión al servicio de usuarios. Los filtros no se envían ni se consultan.";
        };

        filtros.querySelectorAll("input, select").forEach(campo => {
            campo.addEventListener("input", informar);
            campo.addEventListener("change", informar);
        });

        filtros.querySelector("[data-limpiar-usuarios]")?.addEventListener("click", () => {
            filtros.querySelectorAll("input, select").forEach(campo => {
                campo.value = "";
            });
            informar();
            document.getElementById("hu3Busqueda")?.focus();
        });
    }

    const dialogo = document.getElementById("usuario-estado-dialog");
    if (dialogo instanceof HTMLDialogElement) {
        dialogo.querySelector("[data-cancelar-usuario]")?.addEventListener("click", () => dialogo.close());
    }

    document.querySelectorAll("[data-estado-usuario-pendiente]").forEach(campo => {
        campo.indeterminate = true;
    });
})();
