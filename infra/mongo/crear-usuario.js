const database = process.env.MONGO_INITDB_DATABASE;

db.getSiblingDB(database).createUser({
    user: process.env.MONGO_APP_USERNAME,
    pwd: process.env.MONGO_APP_PASSWORD,
    roles: [{ role: "readWrite", db: database }]
});
