# syntax=docker/dockerfile:1
#
# La consola es HTML, CSS y módulos ES servidos tal cual: no hay empaquetador ni paso de
# compilación, así que la imagen es simplemente nginx con los archivos y su configuración.

FROM nginx:1.29-alpine

COPY nginx.conf           /etc/nginx/conf.d/default.conf
COPY cabeceras-proxy.conf /etc/nginx/cabeceras-proxy.conf
COPY index.html           /usr/share/nginx/html/index.html
COPY assets/              /usr/share/nginx/html/assets/

EXPOSE 80

HEALTHCHECK --interval=15s --timeout=4s --start-period=10s --retries=3 \
    CMD wget --quiet --spider http://localhost/ || exit 1
