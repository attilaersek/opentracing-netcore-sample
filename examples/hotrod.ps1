docker run -it --rm --network monitoring --env JAEGER_AGENT_HOST=jaeger --env JAEGER_AGENT_PORT=6831 -p8080-8083:8080-8083 jaegertracing/example-hotrod:latest all --fix-disable-db-conn-mutex --fix-db-query-delay 10ms --fix-route-worker-pool-size 30