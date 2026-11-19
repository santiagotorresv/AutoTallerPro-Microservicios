const { chromium } = require(process.env.PLAYWRIGHT_MODULE_PATH || 'playwright-core');
const path = require('node:path');
(async () => {
 const browser=await chromium.launch({executablePath:process.env.CHROME_PATH,headless:true});
 const page=await browser.newPage({viewport:{width:1440,height:1000}});
 const errores=[];page.on('pageerror',e=>errores.push(e.message));
 let total=0;
 const base=process.env.GRUPO1_FRONTEND_URL || 'http://127.0.0.1:5105';
 for(const modulo of ['Clientes','Vehiculos','Categorias','Servicios','Mecanicos']) {
  for(const operacion of ['Index','Crear','Editar/01234567-0123-0123-0123-012345678901','Eliminar/01234567-0123-0123-0123-012345678901']) {
   const respuesta=await page.goto(`${base}/${modulo}/${operacion}`);
   if(respuesta.status()!==200) throw new Error(`${modulo}/${operacion}: ${respuesta.status()}`);
   if(!await page.locator('h1').innerText()) throw new Error('Falta el título.');
   if(operacion==='Crear') {
    if(await page.locator('input[name="__RequestVerificationToken"]').count()!==1) throw new Error('Falta antifalsificación.');
    const obligatorios=page.locator('input[data-val-required]:not([type=checkbox])');
    if(await obligatorios.count()) {
     await page.getByRole('button',{name:'Guardar',exact:true}).click();
     if(await page.locator('.field-validation-error').count()===0) throw new Error(`Sin validación en ${modulo}`);
    }
    await page.screenshot({path:path.join(process.cwd(),'docs/grupo-1/evidencia',`${modulo.toLowerCase()}-crear.png`),fullPage:true});
   }
   total++;
  }
 }
 await page.setViewportSize({width:390,height:844});
 await page.goto(base+'/Mecanicos/Crear');
 const ancho=await page.evaluate(()=>({viewport:innerWidth,documento:document.documentElement.scrollWidth}));
 await page.screenshot({path:path.join(process.cwd(),'docs/grupo-1/evidencia','mecanicos-movil.png'),fullPage:true});
 await browser.close();
 if(errores.length) throw new Error(errores.join('\n'));
 console.log(JSON.stringify({paginasRevisadas:total,erroresJavaScript:errores.length,anchoMovil:ancho}));
})();
