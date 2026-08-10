### Requisitos de Control de Caja Chica: ###
------------------------------------

# Debe✅: #
- Aceptar archivos de las facturas
- Validar que el efectivo restante + lo que se ha gastado de caja chica = fondo fijo.
- Validar que lo que se vaya a reposicionar no sea más que el fondo fijo inicial.
- Permitir al custodio solicitar una reposición cuando el fondo restante se encuentre entre 30%-20% del total.
- 
-

# No debe🚫: #
- Permitir retirar mas de un 2.5% del fondo fijo por gasto.
- Permitir que otros usuarios fuera del custodio tengan acceso a los fondos.
- 
-
-

# Puede🌱: #
- Generar un PDF con todas las facturas desde la última reposición. Luego, estas pasan al estado de "Reposicionadas" para cerrar el ciclo, pasar al historial sin ser eliminadas, y no ser incluidas en la siguiente reposición.
-
-
-
-

# Roles de usuario y accesos👤: #
- Custodio (responsable): 
	*Registrar gastos y adjuntar facturas.
	*Ejecutar el arqueo mensual.
	*Generar y enviar solicitudes de reposición.

- Admin del sistema:
	*Configurar fondos, límites, categorías de gastos 	y asignación de usuarios.

- Aprobador / Gerente de Área:
	*Revisar gastos e inspeccionar comprobantes.
	*Aprobar o rechazar la solicitud de reposición.

- Finanzas:
	*Procesar el pago de la reposición.
	*Recibir el PDF conjunto con las facturas.
	*Marcar la reposición como "Pagada/Reembolsada".

- Auditor Interno (Read-Only):
	*Consultar historial de reposiciones, arqueos mensuales y reportes de descuadres.
