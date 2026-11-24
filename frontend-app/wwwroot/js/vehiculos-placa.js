(() => {
    "use strict";

    // Ajusta el campo de placa al modo nacional o extranjero. El servidor valida el formato.
    function inicializar(raiz) {
        raiz.querySelectorAll("[data-placa-extranjera]").forEach(interruptor => {
            if (interruptor.dataset.placaInicializada) return;
            interruptor.dataset.placaInicializada = "true";
            const formulario = interruptor.closest("form");
            const placa = formulario?.querySelector("[data-placa-input]");
            const ayuda = formulario?.querySelector("[data-placa-ayuda]");
            if (!placa) {
                return;
            }

            const actualizar = () => {
                const extranjera = interruptor.checked;
                placa.maxLength = extranjera ? 15 : 8;
                placa.placeholder = extranjera ? "Ej. AB123CD" : "Ej. 1234ABC";
                if (ayuda) {
                    ayuda.textContent = extranjera ? "Entre 2 y 15 letras o números." : "3 o 4 números y 3 letras.";
                }
            };

            interruptor.addEventListener("change", actualizar);
            actualizar();
        });
    }
    inicializar(document);
    document.addEventListener("grupo-uno:formulario", evento => inicializar(evento.detail));
})();
