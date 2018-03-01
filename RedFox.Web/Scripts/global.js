axios.defaults.baseURL = 'https://redfox.cloud/'; 
//axios.defaults.baseURL = 'http://localhost/'; 

var redfox = axios.create({
    headers: {
        'Authorization': sessionStorage.getItem('token_type') + ' ' + sessionStorage.getItem('access_token')
    }
});