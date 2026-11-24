(() => {
    "use strict";
    const dialogo = document.querySelector("[data-crud-dialog]");
    const cuerpo = dialogo?.querySelector("[data-crud-body]");
    let peticion;
    let enlaceOrigen;
    let guardando = false;

    function validar(formulario) {
        if (!formulario.reportValidity()) return false;
        return !window.jQuery?.validator || window.jQuery(formulario).valid();
    }

    function mostrar(html, url) {
        const pagina = new DOMParser().parseFromString(html, "text/html");
        const contenido = pagina.querySelector("[data-crud-content]");
        if (!contenido) throw new Error("No se pudo cargar el formulario.");
        cuerpo.replaceChildren(contenido);
        const formulario = cuerpo.querySelector("form");
        if (formulario) {
            formulario.action = new URL(formulario.getAttribute("action") || url, location.origin).href;
            window.jQuery?.validator?.unobtrusive?.parse(formulario);
        }
        const titulo = cuerpo.querySelector("h2");
        if (titulo) {
            titulo.id = "crud-dialogo-titulo";
            dialogo.setAttribute("aria-labelledby", titulo.id);
        }
        cuerpo.querySelector('input:not([type="hidden"]), select, button')?.focus();
    }

    function mostrarError(url) {
        const bloque = document.createElement("div");
        bloque.className = "grupo-dialogo-mensaje";
        const aviso = document.createElement("p");
        aviso.setAttribute("role", "alert");
        aviso.textContent = "No se pudo completar la solicitud. Abre el formulario para revisar el estado o vuelve a intentarlo.";
        const enlace = document.createElement("a");
        enlace.href = url;
        enlace.className = "services-btn services-btn-secondary";
        enlace.dataset.crudFallback = "";
        enlace.textContent = "Abrir formulario";
        bloque.append(aviso, enlace);
        cuerpo.replaceChildren(bloque);
    }

    document.addEventListener("click", async evento => {
        if (!dialogo || !cuerpo) return;
        if (evento.target.closest("[data-crud-close], [data-crud-cancel]") && dialogo.open) {
            evento.preventDefault();
            if (!guardando) dialogo.close();
            return;
        }
        const enlace = evento.target.closest("a[data-crud-modal]");
        if (!enlace || evento.button !== 0 || evento.ctrlKey || evento.metaKey || evento.shiftKey || evento.altKey) return;
        evento.preventDefault();
        enlaceOrigen = enlace;
        peticion?.abort();
        peticion = new AbortController();
        cuerpo.textContent = "Cargando formulario…";
        if (!dialogo.open) dialogo.showModal();
        document.body.classList.add("grupo-dialogo-abierto");
        try {
            const respuesta = await fetch(enlace.href, { credentials: "same-origin", cache: "no-store", signal: peticion.signal });
            if (respuesta.redirected) { location.assign(enlace.href); return; }
            if (!respuesta.ok) throw new Error("No se pudo cargar el formulario.");
            mostrar(await respuesta.text(), enlace.href);
        } catch (error) {
            if (error.name !== "AbortError") mostrarError(enlace.href);
        }
    });

    document.addEventListener("submit", async evento => {
        const formulario = evento.target.closest("form[data-crud-form]");
        if (!formulario) return;
        const enDialogo = dialogo?.open && dialogo.contains(formulario);
        if (enDialogo) evento.preventDefault();
        if (!validar(formulario)) { evento.preventDefault(); return; }
        const boton = formulario.querySelector('button[type="submit"]');
        if (boton?.disabled || guardando) { evento.preventDefault(); return; }
        if (!enDialogo) {
            if (boton) { boton.disabled = true; boton.textContent = "Guardando…"; }
            return;
        }
        guardando = true;
        const cerrar = dialogo.querySelector("[data-crud-close]");
        cerrar.disabled = true;
        if (boton) boton.disabled = true;
        try {
            const respuesta = await fetch(formulario.action, { method: "POST", credentials: "same-origin", body: new FormData(formulario) });
            if (respuesta.redirected) { location.assign(respuesta.url); return; }
            if (!respuesta.ok) throw new Error("No se pudo guardar el registro.");
            mostrar(await respuesta.text(), formulario.action);
        } catch {
            mostrarError(formulario.action);
        } finally {
            guardando = false;
            cerrar.disabled = false;
            if (boton?.isConnected) boton.disabled = false;
        }
    });

    dialogo?.addEventListener("cancel", evento => { if (guardando) evento.preventDefault(); });
    dialogo?.addEventListener("close", () => {
        peticion?.abort();
        document.body.classList.remove("grupo-dialogo-abierto");
        enlaceOrigen?.focus();
    });
})();
