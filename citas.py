import requests
import json
#import warnings
import os
from datetime import datetime, date
#warnings.filterwarnings("ignore")
from bs4 import BeautifulSoup

bcrfechasURL = 'https://bcrcita.bancobcr.com/citas/Home/Appointments_Found_Dates'
bcrhorasURL = 'https://bcrcita.bancobcr.com/citas/Home/GetCitas'
bcrBase = 'https://bcrcita.bancobcr.com/citas/'

responseLog = open('BCRResponse.log', 'a')
sucursalesfile = open('sucursales.json', 'r')
sucursalesjson = sucursalesfile.read()
sucursales = json.loads(sucursalesjson)
cert_path = os.path.join(os.path.dirname(__file__), "bcrcita-bancobcr-com-chain.pem")
sucursalposition = 0
sucursalId = 235
#23 = licencias, pasaportes, DIMEX
#2 = firma digital insumos
#85 = firma digital movil
servicioId = 23
topicoId = 51
tramiteId = 34
mesObjetivo = 6
provinciaObjetivo = 2
sucursalObjetivo = 'Palmares'
token_name = None
token_value = None
myheaders = {'authority': 'bcrcita.bancobcr.com',
'method':'POST',
'path':'/citas/Home/Appointments_Found_Dates',
'scheme':'https',
'accept':'application/json, text/javascript, */*; q=0.01',
'accept-encoding':'gzip, deflate, br, zstd',
'accept-language':'es-ES,es;q=0.9,en;q=0.8',
'origin':'https://bcrcita.bancobcr.com',
'priority':'u=1, i',
'sec-ch-ua-mobile':'?0',
'sec-ch-ua-platform':'Windows',
'sec-fetch-dest':'empty',
'sec-fetch-mode':'cors',
'sec-fetch-site':'same-origin',
'x-requested-with':'XMLHttpRequest',
'Content-Type': 'application/x-www-form-urlencoded; ; charset=UTF-8',

}


myheadersHora = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:134.0) Gecko/20100101 Firefox/134.0',
    'Accept': '*/*',
    'Accept-Language': 'en-US,en;q=0.5',
    'Accept-Encoding': 'gzip, deflate, br, zstd',
    'X-Requested-With': 'XMLHttpRequest',
    'Origin': 'https://bcrcita.bancobcr.com',
    'Sec-Fetch-Dest': 'empty',
    'Sec-Fetch-Mode': 'cors',
    'Sec-Fetch-Site': 'same-origin',
    'Priority': 'u=0',
    'TE': 'trailers',
    'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
}


def filter_sucursales(sucursales, provinciaObjetivo=None, sucursalObjetivo=None):
    if provinciaObjetivo is None and sucursalObjetivo is None:
        return sucursales
    if provinciaObjetivo is not None and sucursalObjetivo is not None:
        return [sucursal for sucursal in sucursales if sucursal['numeroProvincia'] == provinciaObjetivo and sucursalObjetivo in sucursal['pNombre']]
    if provinciaObjetivo is not None:
        return [sucursal for sucursal in sucursales if sucursal['numeroProvincia'] == provinciaObjetivo]
    if sucursalObjetivo is not None:
        return [sucursal for sucursal in sucursales if sucursalObjetivo in sucursal['pNombre']]

def obtenerFechas(servicioId, topicoId, mesObjetivo):
    aggregated_responses = {}

    if 'sucursales' in globals():

        for sucursal in filtered_sucursales:
            sucursalId = sucursal['pSucursalID']
            mybody = f'sucursalId={sucursalId}&servicioId={servicioId}&topicoId={topicoId}'
            try:
                response = requests.post(bcrfechasURL, data=mybody, headers=myheaders, verify=cert_path)                
                response.raise_for_status()
                responsebody = response.json()
            except (requests.RequestException, json.JSONDecodeError) as e:
                print(f"Error fetching or parsing response for sucursal {sucursalId}: {e}")
                continue
            if sucursalId not in aggregated_responses:
                aggregated_responses[sucursalId] = []
            #print(sucursalId)
            for response_item in responsebody:
                dt = datetime.strptime(response_item, "%m/%d/%Y")
                if dt.month == mesObjetivo:
                    aggregated_responses[sucursalId].append(response_item)
                    month_name = dt.strftime("%B")
                    print(f'Cita en {month_name} encontrada: {dt.strftime("%x")} {sucursal["pNombre"]}')
    else:
        print("Error: 'sucursales' is not defined.")
    
    return aggregated_responses

def obtenerHoras(servicioId, topicoId, tramiteId, provinciaId, sucursalId, fechaObtenida):
    global cookies

    try:
        cookies = obtenerCookies()

        data = {
            'pServicioID': servicioId,
            'pTopicoID': topicoId,
            'TramiteId': tramiteId,
            'pProvinciaID': provinciaId,
            'pSucursalID': sucursalId,
            'sFecha': fechaObtenida,
        }

        if token_name is not None and token_value is not None:
            data[token_name] = token_value

        response = requests.post(bcrhorasURL, cookies=cookies, data=data, headers=myheadersHora, verify=cert_path)
        response.raise_for_status()

        soup = BeautifulSoup(response.content, 'html.parser')
        table = soup.find('table', {'id': 'listaInicial'})
        if table is None:
            print(f"No se encontró la tabla de horas para la fecha {fechaObtenida}.")
            return []

        tbody = table.find('tbody')
        if tbody is None:
            print(f"No se encontró el cuerpo de la tabla de horas para la fecha {fechaObtenida}.")
            return []

        date_values = []
        for row in tbody.find_all('tr'):
            date_cell = row.find('td')
            if date_cell is not None:
                date_values.append(date_cell.text.strip())

        return date_values
    except (requests.RequestException, json.JSONDecodeError, AttributeError) as e:
        print(f"Error fetching or parsing response for {fechaObtenida}: {e}")
        return []

def obtenerCookies():
    global token_name, token_value
    try:
        response = requests.get(bcrBase, verify=cert_path)
        response.raise_for_status()
        soup = BeautifulSoup(response.content, 'html.parser')
        token_input = soup.find('input', {'name': '__RequestVerificationToken'})
        token_name = token_input['name']
        token_value = token_input['value']        
    except (requests.RequestException, json.JSONDecodeError) as e:
        print(f"Error fetching or parsing response: {e}")
    return response.cookies            

filtered_sucursales = filter_sucursales(sucursales, provinciaObjetivo, sucursalObjetivo)
aggregated_responses = obtenerFechas(servicioId, topicoId, mesObjetivo)
#print(aggregated_responses)
cookies = obtenerCookies()
all_horas = {}
for sucursalId, fechas in aggregated_responses.items():
    for fecha in fechas:
        dt = datetime.strptime(fecha, "%m/%d/%Y")
        formatted_fecha = dt.strftime("%d/%m/%Y")
        horas = obtenerHoras(servicioId, topicoId, tramiteId, provinciaObjetivo, sucursalId, formatted_fecha)
        all_horas[fecha] = horas

print(all_horas)
